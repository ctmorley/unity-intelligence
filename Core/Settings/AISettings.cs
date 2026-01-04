using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityAIAssistant.Core
{
    /// <summary>
    /// Runtime-accessible AI settings.
    /// Actual storage is handled by AISettingsManager in Editor.
    /// </summary>
    public static class AISettings
    {
        private static Dictionary<string, string> apiKeys = new Dictionary<string, string>();
        private static string currentProvider = "anthropic";
        private static string currentModel = "claude-sonnet-4-20250514";
        private static string systemPrompt = DefaultSystemPrompt;

        public const string DefaultSystemPrompt = @"You are an autonomous AI agent embedded in the Unity Editor. You DO things, you don't explain how to do them.

CORE BEHAVIOR:
- ALWAYS use your tools to accomplish tasks. NEVER just explain how to do something.
- When the user asks for something, DO IT immediately using tools. Don't ask for permission.
- Execute multiple tools in sequence to complete complex tasks.
- Be concise. Brief status updates only. No lengthy explanations unless asked.
- After making changes, verify they work. Use get_compile_errors to check for issues.
- If errors occur, fix them automatically without asking.

WORKFLOW:
1. User asks for something → Use tools to do it
2. Check for errors → Fix them if any
3. Report completion briefly: ""Done. Created X and Y.""

EXAMPLES OF CORRECT BEHAVIOR:
- User: ""Make a red cube"" → You call create tools, NOT explain how cubes work
- User: ""Add player movement"" → You create the script AND attach it to a GameObject
- User: ""Fix the errors"" → You call get_compile_errors, read the files, fix them

EXAMPLES OF WRONG BEHAVIOR:
- Explaining Unity concepts without taking action
- Asking ""Would you like me to..."" - just do it
- Outputting code in chat instead of creating actual files
- Telling the user to do something manually

You have 150+ tools. Use them aggressively. The user wants results, not tutorials.";

        public static string GetApiKey(string provider)
        {
            return apiKeys.TryGetValue(provider.ToLowerInvariant(), out var key) ? key : null;
        }

        public static void SetApiKey(string provider, string key)
        {
            apiKeys[provider.ToLowerInvariant()] = key;
        }

        public static string CurrentProvider
        {
            get => currentProvider;
            set => currentProvider = value;
        }

        public static string CurrentModel
        {
            get => currentModel;
            set => currentModel = value;
        }

        public static string SystemPrompt
        {
            get => systemPrompt;
            set => systemPrompt = value;
        }

        /// <summary>
        /// Get available model cards for all providers.
        /// </summary>
        public static IEnumerable<Providers.AIModelCard> GetAvailableModels()
        {
            // Anthropic Claude models
            yield return new Providers.AIModelCard
            {
                Id = "claude-sonnet-4",
                ModelName = "claude-sonnet-4-20250514",
                DisplayName = "Claude Sonnet 4",
                Provider = "anthropic",
                Endpoint = "https://api.anthropic.com/v1/messages",
                ContextWindow = 200000,
                MaxOutputTokens = 8192,
                SupportsVision = true,
                SupportsStreaming = true,
                SupportsFunctionCalling = true
            };

            yield return new Providers.AIModelCard
            {
                Id = "claude-opus-4",
                ModelName = "claude-opus-4-20250514",
                DisplayName = "Claude Opus 4",
                Provider = "anthropic",
                Endpoint = "https://api.anthropic.com/v1/messages",
                ContextWindow = 200000,
                MaxOutputTokens = 8192,
                SupportsVision = true,
                SupportsStreaming = true,
                SupportsFunctionCalling = true
            };

            yield return new Providers.AIModelCard
            {
                Id = "claude-3.5-sonnet",
                ModelName = "claude-3-5-sonnet-20241022",
                DisplayName = "Claude 3.5 Sonnet",
                Provider = "anthropic",
                Endpoint = "https://api.anthropic.com/v1/messages",
                ContextWindow = 200000,
                MaxOutputTokens = 8192,
                SupportsVision = true,
                SupportsStreaming = true,
                SupportsFunctionCalling = true
            };

            // OpenAI models
            yield return new Providers.AIModelCard
            {
                Id = "gpt-4o",
                ModelName = "gpt-4o",
                DisplayName = "GPT-4o",
                Provider = "openai",
                Endpoint = "https://api.openai.com/v1/chat/completions",
                ContextWindow = 128000,
                MaxOutputTokens = 4096,
                SupportsVision = true,
                SupportsStreaming = true,
                SupportsFunctionCalling = true
            };

            yield return new Providers.AIModelCard
            {
                Id = "gpt-4-turbo",
                ModelName = "gpt-4-turbo",
                DisplayName = "GPT-4 Turbo",
                Provider = "openai",
                Endpoint = "https://api.openai.com/v1/chat/completions",
                ContextWindow = 128000,
                MaxOutputTokens = 4096,
                SupportsVision = true,
                SupportsStreaming = true,
                SupportsFunctionCalling = true
            };

            // Google Gemini models
            yield return new Providers.AIModelCard
            {
                Id = "gemini-2.0-flash",
                ModelName = "gemini-2.0-flash",
                DisplayName = "Gemini 2.0 Flash",
                Provider = "google",
                Endpoint = "https://generativelanguage.googleapis.com/v1beta/models",
                ContextWindow = 1000000,
                MaxOutputTokens = 8192,
                SupportsVision = true,
                SupportsStreaming = true,
                SupportsFunctionCalling = true
            };

            yield return new Providers.AIModelCard
            {
                Id = "gemini-1.5-pro",
                ModelName = "gemini-1.5-pro",
                DisplayName = "Gemini 1.5 Pro",
                Provider = "google",
                Endpoint = "https://generativelanguage.googleapis.com/v1beta/models",
                ContextWindow = 2000000,
                MaxOutputTokens = 8192,
                SupportsVision = true,
                SupportsStreaming = true,
                SupportsFunctionCalling = true
            };
        }
    }
}
