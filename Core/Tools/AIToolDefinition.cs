using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace UnityAIAssistant.Core.Tools
{
    /// <summary>
    /// Definition of an AI-callable tool for function calling.
    /// </summary>
    [Serializable]
    public class AIToolDefinition
    {
        [JsonProperty("name")]
        public string Name;

        [JsonProperty("description")]
        public string Description;

        [JsonProperty("input_schema")]
        public AIToolInputSchema InputSchema;

        /// <summary>Internal method name for execution.</summary>
        [JsonIgnore]
        public string MethodName;

        /// <summary>Whether this tool requires user confirmation before execution.</summary>
        [JsonIgnore]
        public bool RequiresConfirmation;

        public AIToolDefinition(
            string name,
            string description,
            AIToolInputSchema inputSchema,
            string methodName = null,
            bool requiresConfirmation = false)
        {
            Name = name;
            Description = description;
            InputSchema = inputSchema;
            MethodName = methodName ?? name;
            RequiresConfirmation = requiresConfirmation;
        }
    }

    /// <summary>
    /// JSON Schema for tool input parameters.
    /// </summary>
    [Serializable]
    public class AIToolInputSchema
    {
        [JsonProperty("type")]
        public string Type = "object";

        [JsonProperty("properties")]
        public Dictionary<string, AIToolParameterSchema> Properties;

        [JsonProperty("required")]
        public List<string> Required;

        public AIToolInputSchema()
        {
            Properties = new Dictionary<string, AIToolParameterSchema>();
            Required = new List<string>();
        }
    }

    /// <summary>
    /// Schema for a single tool parameter.
    /// </summary>
    [Serializable]
    public class AIToolParameterSchema
    {
        [JsonProperty("type")]
        public string Type;

        [JsonProperty("description")]
        public string Description;

        [JsonProperty("enum", NullValueHandling = NullValueHandling.Ignore)]
        public string[] Enum;

        [JsonProperty("items", NullValueHandling = NullValueHandling.Ignore)]
        public AIToolParameterSchema Items;

        public AIToolParameterSchema(string type, string description, string[] enumValues = null)
        {
            Type = type;
            Description = description;
            Enum = enumValues;
        }
    }

    /// <summary>
    /// Result from executing an AI tool.
    /// </summary>
    [Serializable]
    public class AIToolResult
    {
        public bool Success;
        public string Message;
        public object Data;

        private AIToolResult(bool success, string message, object data = null)
        {
            Success = success;
            Message = message;
            Data = data;
        }

        public static AIToolResult Succeeded(string message, object data = null)
            => new AIToolResult(true, message, data);

        public static AIToolResult Failed(string message, object data = null)
            => new AIToolResult(false, message, data);

        public string ToJson()
        {
            if (Data != null)
            {
                return JsonConvert.SerializeObject(new { success = Success, message = Message, data = Data });
            }
            return JsonConvert.SerializeObject(new { success = Success, message = Message });
        }
    }
}
