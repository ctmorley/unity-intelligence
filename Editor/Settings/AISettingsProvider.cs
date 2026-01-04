using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityAIAssistant.Core;

namespace UnityAIAssistant.Editor.Settings
{
    /// <summary>
    /// Project Settings provider for AI Assistant configuration.
    /// </summary>
    public class AISettingsProvider : SettingsProvider
    {
        private const string SettingsPath = "Project/AI Assistant";

        // EditorPrefs keys (with simple obfuscation)
        private const string AnthropicKeyPref = "AIAssistant_Anthropic_Key";
        private const string OpenAIKeyPref = "AIAssistant_OpenAI_Key";
        private const string GoogleKeyPref = "AIAssistant_Google_Key";
        private const string SystemPromptPref = "AIAssistant_SystemPrompt";

        private string anthropicKey;
        private string openaiKey;
        private string googleKey;
        private string systemPrompt;
        private bool showAnthropicKey;
        private bool showOpenAIKey;
        private bool showGoogleKey;

        public AISettingsProvider(string path, SettingsScope scope = SettingsScope.Project)
            : base(path, scope) { }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            // Load saved settings
            anthropicKey = DecodeKey(EditorPrefs.GetString(AnthropicKeyPref, ""));
            openaiKey = DecodeKey(EditorPrefs.GetString(OpenAIKeyPref, ""));
            googleKey = DecodeKey(EditorPrefs.GetString(GoogleKeyPref, ""));
            systemPrompt = EditorPrefs.GetString(SystemPromptPref, AISettings.DefaultSystemPrompt);

            // Apply to runtime settings
            AISettings.SetApiKey("anthropic", anthropicKey);
            AISettings.SetApiKey("openai", openaiKey);
            AISettings.SetApiKey("google", googleKey);
            AISettings.SystemPrompt = systemPrompt;
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.Space(10);

            // API Keys Section
            EditorGUILayout.LabelField("API Keys", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Enter your API keys for the AI providers you want to use. " +
                "Keys are stored locally in EditorPrefs.",
                MessageType.Info);

            EditorGUILayout.Space(5);

            // Anthropic
            DrawApiKeyField("Anthropic (Claude)", ref anthropicKey, ref showAnthropicKey, AnthropicKeyPref, "anthropic");
            EditorGUILayout.LabelField("", "Get your key at: https://console.anthropic.com/", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            // OpenAI
            DrawApiKeyField("OpenAI (GPT-4)", ref openaiKey, ref showOpenAIKey, OpenAIKeyPref, "openai");
            EditorGUILayout.LabelField("", "Get your key at: https://platform.openai.com/api-keys", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            // Google
            DrawApiKeyField("Google (Gemini)", ref googleKey, ref showGoogleKey, GoogleKeyPref, "google");
            EditorGUILayout.LabelField("", "Get your key at: https://aistudio.google.com/apikey", EditorStyles.miniLabel);

            EditorGUILayout.Space(20);

            // System Prompt Section
            EditorGUILayout.LabelField("System Prompt", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The system prompt sets the behavior and context for the AI assistant.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            systemPrompt = EditorGUILayout.TextArea(systemPrompt, GUILayout.Height(150));
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(SystemPromptPref, systemPrompt);
                AISettings.SystemPrompt = systemPrompt;
            }

            if (GUILayout.Button("Reset to Default", GUILayout.Width(120)))
            {
                systemPrompt = AISettings.DefaultSystemPrompt;
                EditorPrefs.SetString(SystemPromptPref, systemPrompt);
                AISettings.SystemPrompt = systemPrompt;
            }

            EditorGUILayout.Space(20);

            // Status Section
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawStatusRow("Anthropic", !string.IsNullOrEmpty(anthropicKey));
            DrawStatusRow("OpenAI", !string.IsNullOrEmpty(openaiKey));
            DrawStatusRow("Google", !string.IsNullOrEmpty(googleKey));

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Open AI Assistant Window", GUILayout.Height(30)))
            {
                EditorApplication.ExecuteMenuItem("Window/AI Assistant");
            }
        }

        private void DrawApiKeyField(string label, ref string key, ref bool showKey, string prefKey, string provider)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(150));

            EditorGUI.BeginChangeCheck();
            if (showKey)
            {
                key = EditorGUILayout.TextField(key);
            }
            else
            {
                key = EditorGUILayout.PasswordField(key);
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(prefKey, EncodeKey(key));
                AISettings.SetApiKey(provider, key);
            }

            if (GUILayout.Button(showKey ? "Hide" : "Show", GUILayout.Width(50)))
            {
                showKey = !showKey;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatusRow(string provider, bool isConfigured)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(provider, GUILayout.Width(100));

            var style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = isConfigured ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.5f, 0.2f);
            EditorGUILayout.LabelField(isConfigured ? "Configured" : "Not configured", style);

            EditorGUILayout.EndHorizontal();
        }

        // Simple obfuscation (not true encryption, but better than plaintext)
        private static string EncodeKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            var bytes = System.Text.Encoding.UTF8.GetBytes(key);
            return System.Convert.ToBase64String(bytes);
        }

        private static string DecodeKey(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return "";
            try
            {
                var bytes = System.Convert.FromBase64String(encoded);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return "";
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new AISettingsProvider(SettingsPath, SettingsScope.Project)
            {
                keywords = new HashSet<string>(new[] { "AI", "Assistant", "Claude", "OpenAI", "GPT", "Gemini", "API" })
            };
            return provider;
        }
    }
}
