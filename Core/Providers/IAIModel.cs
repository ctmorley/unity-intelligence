using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityAIAssistant.Core.Tools;

namespace UnityAIAssistant.Core.Providers
{
    /// <summary>
    /// Core abstraction for AI model providers (Claude, OpenAI, Gemini).
    /// Supports streaming responses and function calling.
    /// </summary>
    public interface IAIModel
    {
        /// <summary>Fired when a text chunk is received during streaming.</summary>
        event Action<string> OnTextChunk;

        /// <summary>Fired when the text stream is complete.</summary>
        event Action OnTextComplete;

        /// <summary>Fired when the model requests a tool/function call.</summary>
        event Action<AIToolCall> OnToolCall;

        /// <summary>Fired when an error occurs.</summary>
        event Action<string> OnError;

        /// <summary>Fired when a complete turn (text + all tool calls) is finished.</summary>
        event Action OnTurnComplete;

        /// <summary>Fired with token usage information after each response.</summary>
        event Action<TokenUsage> OnTokenUsage;

        /// <summary>Model metadata and capabilities.</summary>
        AIModelCard ModelCard { get; }

        /// <summary>Whether the model supports streaming responses.</summary>
        bool SupportsStreaming { get; }

        /// <summary>Whether the model supports function/tool calling.</summary>
        bool SupportsFunctionCalling { get; }

        /// <summary>Whether the model is ready to accept new requests.</summary>
        bool CanSendRequest { get; }

        /// <summary>Whether the model is currently processing a request.</summary>
        bool IsProcessing { get; }

        /// <summary>
        /// Initialize the model session with available tools.
        /// </summary>
        /// <param name="tools">Tool definitions for function calling.</param>
        /// <returns>Session ID.</returns>
        Task<string> StartAsync(IEnumerable<AIToolDefinition> tools);

        /// <summary>
        /// Called every frame from EditorApplication.update to process async operations.
        /// </summary>
        void Update();

        /// <summary>
        /// Clean up resources and close the session.
        /// </summary>
        void Close();

        /// <summary>
        /// Send a text message to the model.
        /// </summary>
        /// <param name="message">User message text.</param>
        /// <param name="codeContext">Optional code context for RAG.</param>
        void SendText(string message, IEnumerable<CodeContext> codeContext = null);

        /// <summary>
        /// Send tool execution results back to the model.
        /// </summary>
        /// <param name="results">Results from tool executions.</param>
        void SendToolResults(IEnumerable<AIToolCallResult> results);

        /// <summary>
        /// Set the system prompt for the conversation.
        /// </summary>
        /// <param name="systemPrompt">System instructions for the model.</param>
        void SetSystemPrompt(string systemPrompt);

        /// <summary>
        /// Clear conversation history.
        /// </summary>
        void ClearHistory();
    }

    /// <summary>
    /// Metadata about an AI model.
    /// </summary>
    [Serializable]
    public class AIModelCard
    {
        public string Id;
        public string ModelName;
        public string DisplayName;
        public string Provider;  // "anthropic", "openai", "google"
        public string Endpoint;
        public int ContextWindow;
        public int MaxOutputTokens;
        public bool SupportsVision;
        public bool SupportsStreaming;
        public bool SupportsFunctionCalling;
    }

    /// <summary>
    /// Token usage statistics.
    /// </summary>
    [Serializable]
    public struct TokenUsage
    {
        public int InputTokens;
        public int OutputTokens;
        public int CachedTokens;
        public int TotalTokens => InputTokens + OutputTokens;
    }

    /// <summary>
    /// A tool/function call request from the model.
    /// </summary>
    [Serializable]
    public class AIToolCall
    {
        public string Id;
        public string Name;
        public string Arguments;  // JSON string

        public AIToolCall(string id, string name, string arguments)
        {
            Id = id;
            Name = name;
            Arguments = arguments;
        }
    }

    /// <summary>
    /// Result of a tool execution to send back to the model.
    /// </summary>
    [Serializable]
    public class AIToolCallResult
    {
        public string ToolCallId;
        public string Result;
        public bool IsError;

        public AIToolCallResult(string toolCallId, string result, bool isError = false)
        {
            ToolCallId = toolCallId;
            Result = result;
            IsError = isError;
        }
    }

    /// <summary>
    /// Code context for RAG-enhanced prompts.
    /// </summary>
    [Serializable]
    public class CodeContext
    {
        public string FilePath;
        public string Content;
        public float Relevance;
        public int StartLine;
        public int EndLine;
    }
}
