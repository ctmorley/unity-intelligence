using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityAIAssistant.Core.Tools;
using UnityAIAssistant.Core.Providers;

namespace UnityAIAssistant.Editor.Tools
{
    /// <summary>
    /// Discovers and manages AI-callable tools using reflection.
    /// Tools are marked with [AITool] attribute and auto-discovered.
    /// </summary>
    [InitializeOnLoad]
    public static class AIToolManager
    {
        private static readonly Dictionary<string, RegisteredTool> tools = new Dictionary<string, RegisteredTool>();
        private static bool isInitialized;

        static AIToolManager()
        {
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize()
        {
            if (isInitialized) return;
            isInitialized = true;

            DiscoverTools();
            Debug.Log($"[AIToolManager] Discovered {tools.Count} AI tools");
        }

        private static void DiscoverTools()
        {
            tools.Clear();

            // Find all methods with AIToolAttribute
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.FullName.StartsWith("Unity") &&
                           !a.FullName.StartsWith("System") &&
                           !a.FullName.StartsWith("mscorlib") &&
                           !a.FullName.StartsWith("netstandard"));

            foreach (var assembly in assemblies)
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        {
                            var attr = method.GetCustomAttribute<AIToolAttribute>();
                            if (attr != null)
                            {
                                RegisterTool(method, attr);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Skip assemblies that can't be reflected
                }
            }
        }

        private static void RegisterTool(MethodInfo method, AIToolAttribute attr)
        {
            var definition = GenerateDefinition(method, attr);
            var tool = new RegisteredTool
            {
                Method = method,
                Attribute = attr,
                Definition = definition
            };

            tools[attr.ToolName] = tool;
        }

        private static AIToolDefinition GenerateDefinition(MethodInfo method, AIToolAttribute attr)
        {
            var schema = new AIToolInputSchema();
            var parameters = method.GetParameters();

            foreach (var param in parameters)
            {
                var paramAttr = param.GetCustomAttribute<AIToolParameterAttribute>();
                var description = paramAttr?.Description ?? param.Name;
                var jsonType = GetJsonType(param.ParameterType);

                schema.Properties[param.Name] = new AIToolParameterSchema(
                    jsonType,
                    description,
                    paramAttr?.EnumValues
                );

                // Add to required if not optional
                if (paramAttr?.IsOptional != true && !param.HasDefaultValue)
                {
                    schema.Required.Add(param.Name);
                }
            }

            return new AIToolDefinition(
                attr.ToolName,
                attr.Description,
                schema,
                method.Name,
                attr.RequiresConfirmation
            );
        }

        private static string GetJsonType(Type type)
        {
            if (type == typeof(int) || type == typeof(long) ||
                type == typeof(float) || type == typeof(double))
                return "number";
            if (type == typeof(bool))
                return "boolean";
            if (type == typeof(string))
                return "string";
            if (type.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
                return "array";
            return "object";
        }

        /// <summary>
        /// Get all registered tool definitions for AI function calling.
        /// </summary>
        public static IEnumerable<AIToolDefinition> GetToolDefinitions()
        {
            if (!isInitialized) Initialize();
            return tools.Values.Select(t => t.Definition);
        }

        /// <summary>
        /// Execute a tool by name with JSON arguments.
        /// </summary>
        public static AIToolResult ExecuteTool(AIToolCall toolCall)
        {
            if (!isInitialized) Initialize();

            if (!tools.TryGetValue(toolCall.Name, out var tool))
            {
                return AIToolResult.Failed($"Unknown tool: {toolCall.Name}");
            }

            // Check for confirmation requirement
            if (tool.Attribute.RequiresConfirmation)
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Confirm Tool Execution",
                    $"The AI wants to execute: {tool.Attribute.ToolName}\n\n{tool.Attribute.Description}\n\nArguments:\n{toolCall.Arguments}",
                    "Allow",
                    "Deny"
                );

                if (!confirmed)
                {
                    return AIToolResult.Failed("User denied tool execution");
                }
            }

            try
            {
                // Parse arguments
                var args = JObject.Parse(toolCall.Arguments);
                var parameters = tool.Method.GetParameters();
                var invokeArgs = new object[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    if (args.TryGetValue(param.Name, out var value))
                    {
                        invokeArgs[i] = value.ToObject(param.ParameterType);
                    }
                    else if (param.HasDefaultValue)
                    {
                        invokeArgs[i] = param.DefaultValue;
                    }
                    else
                    {
                        return AIToolResult.Failed($"Missing required parameter: {param.Name}");
                    }
                }

                // Invoke the method
                var result = tool.Method.Invoke(null, invokeArgs);

                if (result is AIToolResult toolResult)
                {
                    return toolResult;
                }
                else if (result != null)
                {
                    return AIToolResult.Succeeded(result.ToString());
                }
                else
                {
                    return AIToolResult.Succeeded("Tool executed successfully");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIToolManager] Tool execution error: {ex}");
                return AIToolResult.Failed($"Execution error: {ex.Message}");
            }
        }

        /// <summary>
        /// Force re-discovery of tools (useful after assembly reload).
        /// </summary>
        public static void Refresh()
        {
            isInitialized = false;
            Initialize();
        }

        private class RegisteredTool
        {
            public MethodInfo Method;
            public AIToolAttribute Attribute;
            public AIToolDefinition Definition;
        }
    }
}
