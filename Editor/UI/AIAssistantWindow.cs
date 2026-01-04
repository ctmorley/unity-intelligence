using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityAIAssistant.Core;
using UnityAIAssistant.Core.Providers;
using UnityAIAssistant.Core.Providers.Anthropic;
using UnityAIAssistant.Core.Tools;
using UnityAIAssistant.Editor.Tools;
using UnityAIAssistant.Editor.UI.Elements;

namespace UnityAIAssistant.Editor.UI
{
    /// <summary>
    /// Main EditorWindow for the AI Assistant chat interface.
    /// </summary>
    public class AIAssistantWindow : EditorWindow
    {
        private ScrollView chatScrollView;
        private TextField inputField;
        private Button sendButton;
        private DropdownField modelSelector;
        private VisualElement thinkingIndicator;
        private Label statusLabel;

        private IAIModel currentModel;
        private List<ChatMessageElement> messageElements = new List<ChatMessageElement>();
        private ChatMessageElement streamingMessage;
        private bool isInitialized;

        [MenuItem("Window/AI Assistant %#a")]
        public static void ShowWindow()
        {
            var window = GetWindow<AIAssistantWindow>();
            window.titleContent = new GUIContent("AI Assistant");
            window.minSize = new Vector2(400, 300);
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            currentModel?.Close();
        }

        private void CreateGUI()
        {
            // Build UI programmatically (can be replaced with UXML later)
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1;

            // Apply dark theme styling
            root.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);

            // Header with model selector
            var header = CreateHeader();
            root.Add(header);

            // Chat scroll area
            chatScrollView = new ScrollView(ScrollViewMode.Vertical);
            chatScrollView.style.flexGrow = 1;
            chatScrollView.style.paddingLeft = 10;
            chatScrollView.style.paddingRight = 10;
            chatScrollView.style.paddingTop = 10;
            chatScrollView.style.paddingBottom = 10;
            root.Add(chatScrollView);

            // Thinking indicator
            thinkingIndicator = CreateThinkingIndicator();
            thinkingIndicator.style.display = DisplayStyle.None;
            root.Add(thinkingIndicator);

            // Input area
            var inputArea = CreateInputArea();
            root.Add(inputArea);

            // Status bar
            var statusBar = CreateStatusBar();
            root.Add(statusBar);

            // Initialize
            RefreshModelList();
            isInitialized = true;
        }

        private VisualElement CreateHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 10;
            header.style.paddingRight = 10;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);

            var label = new Label("Model:");
            label.style.color = Color.white;
            label.style.marginRight = 8;
            header.Add(label);

            modelSelector = new DropdownField();
            modelSelector.style.flexGrow = 1;
            modelSelector.style.maxWidth = 250;
            modelSelector.RegisterValueChangedCallback(OnModelChanged);
            header.Add(modelSelector);

            var settingsButton = new Button(OpenSettings) { text = "Settings" };
            settingsButton.style.marginLeft = 10;
            header.Add(settingsButton);

            var clearButton = new Button(ClearChat) { text = "Clear" };
            clearButton.style.marginLeft = 5;
            header.Add(clearButton);

            return header;
        }

        private VisualElement CreateThinkingIndicator()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.paddingLeft = 15;
            container.style.paddingBottom = 5;

            var label = new Label("Thinking...");
            label.style.color = new Color(0.6f, 0.6f, 0.6f);
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            container.Add(label);

            return container;
        }

        private VisualElement CreateInputArea()
        {
            var inputArea = new VisualElement();
            inputArea.style.flexDirection = FlexDirection.Row;
            inputArea.style.paddingLeft = 10;
            inputArea.style.paddingRight = 10;
            inputArea.style.paddingTop = 8;
            inputArea.style.paddingBottom = 8;
            inputArea.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            inputArea.style.borderTopWidth = 1;
            inputArea.style.borderTopColor = new Color(0.1f, 0.1f, 0.1f);

            inputField = new TextField();
            inputField.style.flexGrow = 1;
            inputField.style.marginRight = 8;
            inputField.multiline = true;
            inputField.style.maxHeight = 100;
            inputField.style.whiteSpace = WhiteSpace.Normal;
            inputField.RegisterCallback<KeyDownEvent>(OnInputKeyDown);
            inputArea.Add(inputField);

            sendButton = new Button(OnSendClicked) { text = "Send" };
            sendButton.style.width = 60;
            sendButton.style.height = 30;
            inputArea.Add(sendButton);

            return inputArea;
        }

        private VisualElement CreateStatusBar()
        {
            var statusBar = new VisualElement();
            statusBar.style.flexDirection = FlexDirection.Row;
            statusBar.style.paddingLeft = 10;
            statusBar.style.paddingRight = 10;
            statusBar.style.paddingTop = 4;
            statusBar.style.paddingBottom = 4;
            statusBar.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);

            statusLabel = new Label("Ready");
            statusLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            statusLabel.style.fontSize = 10;
            statusBar.Add(statusLabel);

            return statusBar;
        }

        private void RefreshModelList()
        {
            var models = AISettings.GetAvailableModels().ToList();
            modelSelector.choices = models.Select(m => m.DisplayName).ToList();

            // Select current model
            var currentCard = models.FirstOrDefault(m => m.ModelName == AISettings.CurrentModel);
            if (currentCard != null)
            {
                modelSelector.value = currentCard.DisplayName;
            }
            else if (models.Count > 0)
            {
                modelSelector.value = models[0].DisplayName;
                AISettings.CurrentModel = models[0].ModelName;
                AISettings.CurrentProvider = models[0].Provider;
            }
        }

        private void OnModelChanged(ChangeEvent<string> evt)
        {
            var models = AISettings.GetAvailableModels().ToList();
            var selected = models.FirstOrDefault(m => m.DisplayName == evt.newValue);
            if (selected != null)
            {
                AISettings.CurrentModel = selected.ModelName;
                AISettings.CurrentProvider = selected.Provider;
                statusLabel.text = $"Switched to {selected.DisplayName}";

                // Close current model and create new one on next send
                currentModel?.Close();
                currentModel = null;
            }
        }

        private void OnInputKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return && !evt.shiftKey)
            {
                evt.StopPropagation();
                evt.PreventDefault();
                OnSendClicked();
            }
        }

        private async void OnSendClicked()
        {
            string message = inputField.value?.Trim();
            if (string.IsNullOrEmpty(message)) return;

            inputField.value = "";

            // Add user message to chat
            AddMessage(ChatRole.User, message);

            // Initialize model if needed
            if (currentModel == null)
            {
                try
                {
                    currentModel = CreateModel();
                    SubscribeToModelEvents(currentModel);

                    // Get available tools
                    var tools = AIToolManager.GetToolDefinitions();
                    await currentModel.StartAsync(tools);
                }
                catch (Exception ex)
                {
                    AddMessage(ChatRole.System, $"Error: {ex.Message}");
                    statusLabel.text = "Error initializing model";
                    return;
                }
            }

            // Show thinking indicator
            thinkingIndicator.style.display = DisplayStyle.Flex;
            sendButton.SetEnabled(false);
            statusLabel.text = "Thinking...";

            // Create streaming message placeholder
            streamingMessage = AddMessage(ChatRole.Assistant, "");

            // Send message
            currentModel.SendText(message);
        }

        private IAIModel CreateModel()
        {
            var models = AISettings.GetAvailableModels().ToList();
            var modelCard = models.FirstOrDefault(m => m.ModelName == AISettings.CurrentModel);

            if (modelCard == null)
            {
                throw new InvalidOperationException("No model selected");
            }

            // Create appropriate model based on provider
            switch (modelCard.Provider.ToLowerInvariant())
            {
                case "anthropic":
                    var claude = new ClaudeModel(modelCard);
                    claude.SetSystemPrompt(AISettings.SystemPrompt);
                    return claude;

                case "openai":
                    throw new NotImplementedException("OpenAI provider coming soon");

                case "google":
                    throw new NotImplementedException("Google Gemini provider coming soon");

                default:
                    throw new InvalidOperationException($"Unknown provider: {modelCard.Provider}");
            }
        }

        private void SubscribeToModelEvents(IAIModel model)
        {
            model.OnTextChunk += OnTextChunk;
            model.OnTextComplete += OnTextComplete;
            model.OnToolCall += OnToolCall;
            model.OnError += OnError;
            model.OnTurnComplete += OnTurnComplete;
            model.OnTokenUsage += OnTokenUsage;
        }

        private void OnTextChunk(string chunk)
        {
            streamingMessage?.AppendText(chunk);
            ScrollToBottom();
        }

        private void OnTextComplete()
        {
            streamingMessage?.EndStreaming();
        }

        private void OnToolCall(AIToolCall toolCall)
        {
            // Execute tool and send result back
            statusLabel.text = $"Executing tool: {toolCall.Name}";

            try
            {
                var result = AIToolManager.ExecuteTool(toolCall);
                var results = new[] { new AIToolCallResult(toolCall.Id, result.ToJson(), !result.Success) };
                currentModel.SendToolResults(results);
            }
            catch (Exception ex)
            {
                var results = new[] { new AIToolCallResult(toolCall.Id, $"Error: {ex.Message}", true) };
                currentModel.SendToolResults(results);
            }
        }

        private void OnError(string error)
        {
            thinkingIndicator.style.display = DisplayStyle.None;
            sendButton.SetEnabled(true);
            statusLabel.text = $"Error: {error}";

            if (streamingMessage != null)
            {
                streamingMessage.AppendText($"\n\n**Error:** {error}");
                streamingMessage.EndStreaming();
                streamingMessage = null;
            }
        }

        private void OnTurnComplete()
        {
            thinkingIndicator.style.display = DisplayStyle.None;
            sendButton.SetEnabled(true);
            statusLabel.text = "Ready";
            streamingMessage = null;
        }

        private void OnTokenUsage(TokenUsage usage)
        {
            statusLabel.text = $"Tokens: {usage.InputTokens} in / {usage.OutputTokens} out";
        }

        private ChatMessageElement AddMessage(ChatRole role, string content)
        {
            var element = new ChatMessageElement(role, content);
            chatScrollView.Add(element);
            messageElements.Add(element);
            ScrollToBottom();
            return element;
        }

        private void ScrollToBottom()
        {
            chatScrollView.schedule.Execute(() =>
            {
                chatScrollView.scrollOffset = new Vector2(0, float.MaxValue);
            });
        }

        private void ClearChat()
        {
            chatScrollView.Clear();
            messageElements.Clear();
            currentModel?.ClearHistory();
            statusLabel.text = "Chat cleared";
        }

        private void OpenSettings()
        {
            SettingsService.OpenProjectSettings("Project/AI Assistant");
        }

        private void OnEditorUpdate()
        {
            if (!isInitialized) return;
            currentModel?.Update();
        }
    }

    public enum ChatRole
    {
        User,
        Assistant,
        System
    }
}
