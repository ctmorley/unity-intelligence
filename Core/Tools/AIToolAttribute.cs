using System;

namespace UnityAIAssistant.Core.Tools
{
    /// <summary>
    /// Marks a method as an AI-callable tool.
    /// Methods must be static and return AIToolResult.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class AIToolAttribute : Attribute
    {
        public readonly string ToolName;
        public readonly string Description;
        public readonly bool RequiresConfirmation;

        /// <summary>
        /// Mark a method as an AI-callable tool.
        /// </summary>
        /// <param name="toolName">Name of the tool (snake_case recommended).</param>
        /// <param name="description">Description for the AI to understand when to use this tool.</param>
        /// <param name="requiresConfirmation">Whether to show confirmation dialog before execution.</param>
        public AIToolAttribute(string toolName, string description, bool requiresConfirmation = false)
        {
            ToolName = toolName;
            Description = description;
            RequiresConfirmation = requiresConfirmation;
        }
    }

    /// <summary>
    /// Describes a tool parameter for AI function calling.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class AIToolParameterAttribute : Attribute
    {
        public readonly string Description;
        public readonly string[] EnumValues;
        public readonly bool IsOptional;

        /// <summary>
        /// Describe a tool parameter.
        /// </summary>
        /// <param name="description">Description for the AI to understand this parameter.</param>
        /// <param name="enumType">Optional enum type for constrained values.</param>
        /// <param name="isOptional">Whether this parameter is optional.</param>
        public AIToolParameterAttribute(string description, Type enumType = null, bool isOptional = false)
        {
            Description = description;
            EnumValues = enumType?.IsEnum == true ? Enum.GetNames(enumType) : null;
            IsOptional = isOptional;
        }

        /// <summary>
        /// Describe a tool parameter with explicit enum values.
        /// </summary>
        /// <param name="description">Description for the AI to understand this parameter.</param>
        /// <param name="enumValues">Explicit list of allowed values.</param>
        /// <param name="isOptional">Whether this parameter is optional.</param>
        public AIToolParameterAttribute(string description, string[] enumValues, bool isOptional = false)
        {
            Description = description;
            EnumValues = enumValues;
            IsOptional = isOptional;
        }
    }
}
