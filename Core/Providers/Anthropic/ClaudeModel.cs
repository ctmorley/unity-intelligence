using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityAIAssistant.Core.Streaming;
using UnityAIAssistant.Core.Tools;

namespace UnityAIAssistant.Core.Providers.Anthropic
{
    /// <summary>
    /// Claude AI model implementation using the Anthropic Messages API with streaming.
    /// </summary>
    public class ClaudeModel : IAIModel
    {
        private const string API_URL = "https://api.anthropic.com/v1/messages";
        private const string API_VERSION = "2023-06-01";

        public event Action<string> OnTextChunk;
        public event Action OnTextComplete;
        public event Action<AIToolCall> OnToolCall;
        public event Action<string> OnError;
        public event Action OnTurnComplete;
        public event Action<TokenUsage> OnTokenUsage;

        public AIModelCard ModelCard { get; }
        public bool SupportsStreaming => true;
        public bool SupportsFunctionCalling => true;
        public bool CanSendRequest => !IsProcessing && !string.IsNullOrEmpty(apiKey);
        public bool IsProcessing { get; private set; }

        private string apiKey;
        private string systemPrompt;
        private readonly List<ClaudeMessage> conversationHistory = new List<ClaudeMessage>();
        private List<object> tools;

        private UnityWebRequest activeRequest;
        private SSEStreamHandler streamHandler;
        private StringBuilder currentTextContent;
        private List<AIToolCall> pendingToolCalls;
        private bool responseComplete;

        public ClaudeModel(AIModelCard modelCard)
        {
            ModelCard = modelCard;
        }

        public Task<string> StartAsync(IEnumerable<AIToolDefinition> toolDefinitions)
        {
            apiKey = AISettings.GetApiKey("anthropic");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException(
                    "Anthropic API key not configured. Set it in Project Settings > AI Assistant.");
            }

            // Convert tool definitions to Claude format
            tools = toolDefinitions?.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                input_schema = t.InputSchema
            }).Cast<object>().ToList() ?? new List<object>();

            string sessionId = $"claude-{DateTime.Now:yyyyMMdd-HHmmss}";
            Debug.Log($"[ClaudeModel] Started session: {sessionId}");
            return Task.FromResult(sessionId);
        }

        public void SetSystemPrompt(string prompt)
        {
            systemPrompt = prompt;
        }

        public void ClearHistory()
        {
            conversationHistory.Clear();
        }

        public void SendText(string message, IEnumerable<CodeContext> codeContext = null)
        {
            if (IsProcessing)
            {
                Debug.LogWarning("[ClaudeModel] Already processing a request");
                return;
            }

            // Build user content
            var userContent = BuildUserContent(message, codeContext);
            conversationHistory.Add(new ClaudeMessage("user", userContent));

            StartStreamingRequest();
        }

        public void SendToolResults(IEnumerable<AIToolCallResult> results)
        {
            if (IsProcessing)
            {
                Debug.LogWarning("[ClaudeModel] Already processing a request");
                return;
            }

            // Add tool results as assistant + user messages
            var toolResultContent = results.Select(r => new
            {
                type = "tool_result",
                tool_use_id = r.ToolCallId,
                content = r.Result,
                is_error = r.IsError
            }).Cast<object>().ToList();

            conversationHistory.Add(new ClaudeMessage("user", toolResultContent));
            StartStreamingRequest();
        }

        private object BuildUserContent(string message, IEnumerable<CodeContext> codeContext)
        {
            if (codeContext == null || !codeContext.Any())
            {
                return message;
            }

            // Build content with code context
            var parts = new List<object>();

            // Add code context first
            foreach (var ctx in codeContext)
            {
                parts.Add(new
                {
                    type = "text",
                    text = $"<file path=\"{ctx.FilePath}\">\n{ctx.Content}\n</file>"
                });
            }

            // Add user message
            parts.Add(new
            {
                type = "text",
                text = message
            });

            return parts;
        }

        private void StartStreamingRequest()
        {
            IsProcessing = true;
            currentTextContent = new StringBuilder();
            pendingToolCalls = new List<AIToolCall>();
            responseComplete = false;

            var request = new ClaudeMessagesRequest
            {
                model = ModelCard.ModelName,
                max_tokens = ModelCard.MaxOutputTokens > 0 ? ModelCard.MaxOutputTokens : 4096,
                stream = true,
                system = systemPrompt,
                messages = conversationHistory.ToArray(),
                tools = tools?.Count > 0 ? tools.ToArray() : null
            };

            string json = JsonConvert.SerializeObject(request, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            // Create streaming request
            activeRequest = new UnityWebRequest(API_URL, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            activeRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);

            streamHandler = new SSEStreamHandler(OnSSEEvent);
            activeRequest.downloadHandler = streamHandler;

            activeRequest.SetRequestHeader("Content-Type", "application/json");
            activeRequest.SetRequestHeader("x-api-key", apiKey);
            activeRequest.SetRequestHeader("anthropic-version", API_VERSION);

            activeRequest.SendWebRequest();
            Debug.Log($"[ClaudeModel] Sent request, message count: {conversationHistory.Count}");
        }

        public void Update()
        {
            if (activeRequest == null) return;

            // Process any pending SSE events
            streamHandler?.ProcessPendingEvents();

            // Check if request is complete
            if (activeRequest.isDone)
            {
                if (activeRequest.result != UnityWebRequest.Result.Success)
                {
                    string error = activeRequest.error;
                    if (activeRequest.downloadHandler?.text != null)
                    {
                        try
                        {
                            var errorResponse = JObject.Parse(activeRequest.downloadHandler.text);
                            error = errorResponse["error"]?["message"]?.ToString() ?? error;
                        }
                        catch { }
                    }
                    Debug.LogError($"[ClaudeModel] Request failed: {error}");
                    OnError?.Invoke(error);
                }

                CleanupRequest();
            }
        }

        private void OnSSEEvent(SSEEvent evt)
        {
            if (evt.Type == "done" || evt.Data == "[DONE]")
            {
                FinalizeResponse();
                return;
            }

            try
            {
                var data = JObject.Parse(evt.Data);
                string eventType = data["type"]?.ToString();

                switch (eventType)
                {
                    case "content_block_start":
                        HandleContentBlockStart(data);
                        break;

                    case "content_block_delta":
                        HandleContentBlockDelta(data);
                        break;

                    case "content_block_stop":
                        HandleContentBlockStop(data);
                        break;

                    case "message_delta":
                        HandleMessageDelta(data);
                        break;

                    case "message_stop":
                        FinalizeResponse();
                        break;

                    case "error":
                        var errorMsg = data["error"]?["message"]?.ToString() ?? "Unknown error";
                        OnError?.Invoke(errorMsg);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClaudeModel] Error parsing SSE event: {ex.Message}\nData: {evt.Data}");
            }
        }

        private string currentToolUseId;
        private string currentToolName;
        private StringBuilder currentToolInput;

        private void HandleContentBlockStart(JObject data)
        {
            var contentBlock = data["content_block"];
            string blockType = contentBlock?["type"]?.ToString();

            if (blockType == "tool_use")
            {
                currentToolUseId = contentBlock["id"]?.ToString();
                currentToolName = contentBlock["name"]?.ToString();
                currentToolInput = new StringBuilder();
            }
        }

        private void HandleContentBlockDelta(JObject data)
        {
            var delta = data["delta"];
            string deltaType = delta?["type"]?.ToString();

            switch (deltaType)
            {
                case "text_delta":
                    string text = delta["text"]?.ToString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        currentTextContent.Append(text);
                        OnTextChunk?.Invoke(text);
                    }
                    break;

                case "input_json_delta":
                    string partialJson = delta["partial_json"]?.ToString();
                    if (!string.IsNullOrEmpty(partialJson))
                    {
                        currentToolInput?.Append(partialJson);
                    }
                    break;
            }
        }

        private void HandleContentBlockStop(JObject data)
        {
            // If we were building a tool call, finalize it
            if (!string.IsNullOrEmpty(currentToolUseId))
            {
                var toolCall = new AIToolCall(
                    currentToolUseId,
                    currentToolName,
                    currentToolInput?.ToString() ?? "{}"
                );
                pendingToolCalls.Add(toolCall);

                // Reset tool state
                currentToolUseId = null;
                currentToolName = null;
                currentToolInput = null;
            }
            else
            {
                // Text content complete
                OnTextComplete?.Invoke();
            }
        }

        private void HandleMessageDelta(JObject data)
        {
            var usage = data["usage"];
            if (usage != null)
            {
                var tokenUsage = new TokenUsage
                {
                    InputTokens = usage["input_tokens"]?.Value<int>() ?? 0,
                    OutputTokens = usage["output_tokens"]?.Value<int>() ?? 0
                };
                OnTokenUsage?.Invoke(tokenUsage);
            }
        }

        private void FinalizeResponse()
        {
            if (responseComplete) return;
            responseComplete = true;

            // Add assistant message to history
            var assistantContent = new List<object>();

            if (currentTextContent.Length > 0)
            {
                assistantContent.Add(new { type = "text", text = currentTextContent.ToString() });
            }

            foreach (var toolCall in pendingToolCalls)
            {
                assistantContent.Add(new
                {
                    type = "tool_use",
                    id = toolCall.Id,
                    name = toolCall.Name,
                    input = JObject.Parse(toolCall.Arguments)
                });
            }

            if (assistantContent.Count > 0)
            {
                conversationHistory.Add(new ClaudeMessage("assistant", assistantContent));
            }

            // Fire tool calls
            foreach (var toolCall in pendingToolCalls)
            {
                OnToolCall?.Invoke(toolCall);
            }

            OnTurnComplete?.Invoke();
        }

        private void CleanupRequest()
        {
            IsProcessing = false;
            activeRequest?.Dispose();
            activeRequest = null;
            streamHandler = null;
        }

        public void Close()
        {
            if (activeRequest != null)
            {
                activeRequest.Abort();
                CleanupRequest();
            }
            conversationHistory.Clear();
        }
    }

    #region Internal Types

    [Serializable]
    internal class ClaudeMessagesRequest
    {
        public string model;
        public int max_tokens;
        public bool stream;
        public string system;
        public ClaudeMessage[] messages;
        public object[] tools;
    }

    [Serializable]
    internal class ClaudeMessage
    {
        public string role;
        public object content;

        public ClaudeMessage(string role, object content)
        {
            this.role = role;
            this.content = content;
        }
    }

    #endregion
}
