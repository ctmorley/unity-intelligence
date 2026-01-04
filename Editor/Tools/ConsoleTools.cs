using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityAIAssistant.Core.Tools;

namespace UnityAIAssistant.Editor.Tools
{
    /// <summary>
    /// AI tools for reading Unity console logs and compilation errors.
    /// Gives AI context to automatically diagnose and fix issues.
    /// </summary>
    public static class ConsoleTools
    {
        // Cache reflection info for LogEntries (internal Unity class)
        private static Type _logEntriesType;
        private static MethodInfo _getCountMethod;
        private static MethodInfo _getEntryInternalMethod;
        private static MethodInfo _clearMethod;
        private static Type _logEntryType;
        private static FieldInfo _messageField;
        private static FieldInfo _fileField;
        private static FieldInfo _lineField;
        private static FieldInfo _modeField;

        static ConsoleTools()
        {
            InitializeReflection();
        }

        private static void InitializeReflection()
        {
            try
            {
                // Get LogEntries type (internal)
                _logEntriesType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries");
                if (_logEntriesType != null)
                {
                    _getCountMethod = _logEntriesType.GetMethod("GetCount", BindingFlags.Public | BindingFlags.Static);
                    _clearMethod = _logEntriesType.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static);
                    _getEntryInternalMethod = _logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Public | BindingFlags.Static);
                }

                // Get LogEntry type (internal)
                _logEntryType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntry");
                if (_logEntryType != null)
                {
                    _messageField = _logEntryType.GetField("message", BindingFlags.Public | BindingFlags.Instance);
                    _fileField = _logEntryType.GetField("file", BindingFlags.Public | BindingFlags.Instance);
                    _lineField = _logEntryType.GetField("line", BindingFlags.Public | BindingFlags.Instance);
                    _modeField = _logEntryType.GetField("mode", BindingFlags.Public | BindingFlags.Instance);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ConsoleTools] Failed to initialize reflection: {e.Message}");
            }
        }

        [AITool("get_console_logs", "Get recent console log entries (errors, warnings, and messages)")]
        public static AIToolResult GetConsoleLogs(
            [AIToolParameter("Maximum number of entries to return (default: 50)", isOptional: true)] int maxEntries = 50,
            [AIToolParameter("Filter by type: 'all', 'error', 'warning', 'log' (default: all)", isOptional: true)] string filter = "all")
        {
            try
            {
                if (_logEntriesType == null || _logEntryType == null)
                {
                    return AIToolResult.Failed("Console log access not available in this Unity version");
                }

                int count = (int)_getCountMethod.Invoke(null, null);
                if (count == 0)
                {
                    return AIToolResult.Succeeded("Console is empty - no log entries.");
                }

                var entries = new List<object>();
                var logEntry = Activator.CreateInstance(_logEntryType);
                int retrieved = 0;

                // Read entries from most recent
                for (int i = count - 1; i >= 0 && retrieved < maxEntries; i--)
                {
                    _getEntryInternalMethod.Invoke(null, new object[] { i, logEntry });

                    string message = _messageField?.GetValue(logEntry) as string ?? "";
                    string file = _fileField?.GetValue(logEntry) as string ?? "";
                    int line = _lineField != null ? (int)_lineField.GetValue(logEntry) : 0;
                    int mode = _modeField != null ? (int)_modeField.GetValue(logEntry) : 0;

                    // Determine entry type from mode flags
                    string entryType = GetEntryType(mode);

                    // Apply filter
                    if (filter != "all" && !entryType.ToLower().Contains(filter.ToLower()))
                        continue;

                    entries.Add(new
                    {
                        type = entryType,
                        message = message,
                        file = string.IsNullOrEmpty(file) ? null : file,
                        line = line > 0 ? line : (int?)null
                    });
                    retrieved++;
                }

                if (entries.Count == 0)
                {
                    return AIToolResult.Succeeded($"No {filter} entries found in console.");
                }

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(new
                {
                    totalCount = count,
                    returned = entries.Count,
                    filter = filter,
                    entries = entries
                }, Formatting.Indented));
            }
            catch (Exception e)
            {
                return AIToolResult.Failed($"Failed to read console logs: {e.Message}");
            }
        }

        [AITool("get_console_errors", "Get all error entries from the console (compile errors, runtime errors, exceptions)")]
        public static AIToolResult GetConsoleErrors(
            [AIToolParameter("Maximum number of errors to return (default: 20)", isOptional: true)] int maxEntries = 20)
        {
            return GetConsoleLogs(maxEntries, "error");
        }

        [AITool("get_console_warnings", "Get all warning entries from the console")]
        public static AIToolResult GetConsoleWarnings(
            [AIToolParameter("Maximum number of warnings to return (default: 20)", isOptional: true)] int maxEntries = 20)
        {
            return GetConsoleLogs(maxEntries, "warning");
        }

        [AITool("get_compile_errors", "Get current script compilation errors with file paths and line numbers")]
        public static AIToolResult GetCompileErrors()
        {
            try
            {
                var errors = new List<object>();

                // Get compilation messages from CompilationPipeline
                var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);

                // Also check editor assemblies
                var editorAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);

                // Use EditorUtility to check for compile errors
                bool isCompiling = EditorApplication.isCompiling;

                // Read errors from console that look like compile errors
                if (_logEntriesType != null && _logEntryType != null)
                {
                    int count = (int)_getCountMethod.Invoke(null, null);
                    var logEntry = Activator.CreateInstance(_logEntryType);

                    for (int i = 0; i < count; i++)
                    {
                        _getEntryInternalMethod.Invoke(null, new object[] { i, logEntry });

                        string message = _messageField?.GetValue(logEntry) as string ?? "";
                        string file = _fileField?.GetValue(logEntry) as string ?? "";
                        int line = _lineField != null ? (int)_lineField.GetValue(logEntry) : 0;
                        int mode = _modeField != null ? (int)_modeField.GetValue(logEntry) : 0;

                        // Check if it's an error (mode flags)
                        if (!IsError(mode)) continue;

                        // Check if it looks like a compile error (has CS#### code or file reference)
                        bool isCompileError = message.Contains("error CS") ||
                                            message.Contains("error cs") ||
                                            (file.EndsWith(".cs") && line > 0);

                        if (isCompileError)
                        {
                            errors.Add(new
                            {
                                message = message,
                                file = string.IsNullOrEmpty(file) ? ParseFileFromMessage(message) : file,
                                line = line > 0 ? line : ParseLineFromMessage(message),
                                errorCode = ParseErrorCode(message)
                            });
                        }
                    }
                }

                if (errors.Count == 0)
                {
                    return AIToolResult.Succeeded(JsonConvert.SerializeObject(new
                    {
                        hasErrors = false,
                        isCompiling = isCompiling,
                        message = isCompiling ? "Scripts are currently compiling..." : "No compile errors found!"
                    }, Formatting.Indented));
                }

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(new
                {
                    hasErrors = true,
                    isCompiling = isCompiling,
                    errorCount = errors.Count,
                    errors = errors
                }, Formatting.Indented));
            }
            catch (Exception e)
            {
                return AIToolResult.Failed($"Failed to get compile errors: {e.Message}");
            }
        }

        [AITool("get_compilation_status", "Check if Unity is currently compiling scripts")]
        public static AIToolResult GetCompilationStatus()
        {
            try
            {
                bool isCompiling = EditorApplication.isCompiling;
                bool isUpdating = EditorApplication.isUpdating;

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(new
                {
                    isCompiling = isCompiling,
                    isUpdating = isUpdating,
                    status = isCompiling ? "Compiling scripts..." :
                            isUpdating ? "Updating assets..." : "Ready"
                }, Formatting.Indented));
            }
            catch (Exception e)
            {
                return AIToolResult.Failed($"Failed to get compilation status: {e.Message}");
            }
        }

        [AITool("clear_console", "Clear all entries from the Unity console")]
        public static AIToolResult ClearConsole()
        {
            try
            {
                if (_clearMethod != null)
                {
                    _clearMethod.Invoke(null, null);
                    return AIToolResult.Succeeded("Console cleared.");
                }
                else
                {
                    // Fallback - try the menu item
                    EditorApplication.ExecuteMenuItem("Edit/Clear Console");
                    return AIToolResult.Succeeded("Console cleared via menu item.");
                }
            }
            catch (Exception e)
            {
                return AIToolResult.Failed($"Failed to clear console: {e.Message}");
            }
        }

        [AITool("get_last_exception", "Get the most recent exception/error from the console with full stack trace")]
        public static AIToolResult GetLastException()
        {
            try
            {
                if (_logEntriesType == null || _logEntryType == null)
                {
                    return AIToolResult.Failed("Console log access not available");
                }

                int count = (int)_getCountMethod.Invoke(null, null);
                var logEntry = Activator.CreateInstance(_logEntryType);

                // Search from most recent for an error/exception
                for (int i = count - 1; i >= 0; i--)
                {
                    _getEntryInternalMethod.Invoke(null, new object[] { i, logEntry });

                    string message = _messageField?.GetValue(logEntry) as string ?? "";
                    string file = _fileField?.GetValue(logEntry) as string ?? "";
                    int line = _lineField != null ? (int)_lineField.GetValue(logEntry) : 0;
                    int mode = _modeField != null ? (int)_modeField.GetValue(logEntry) : 0;

                    if (IsError(mode) || IsException(mode))
                    {
                        return AIToolResult.Succeeded(JsonConvert.SerializeObject(new
                        {
                            type = GetEntryType(mode),
                            message = message,
                            file = string.IsNullOrEmpty(file) ? null : file,
                            line = line > 0 ? line : (int?)null,
                            hasStackTrace = message.Contains("\n") || message.Contains("at ")
                        }, Formatting.Indented));
                    }
                }

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(new
                {
                    found = false,
                    message = "No exceptions or errors found in console."
                }, Formatting.Indented));
            }
            catch (Exception e)
            {
                return AIToolResult.Failed($"Failed to get last exception: {e.Message}");
            }
        }

        #region Helper Methods

        private static string GetEntryType(int mode)
        {
            // Unity console mode flags (from internal enum)
            // These are bit flags
            const int kError = 1 << 0;          // 1
            const int kAssert = 1 << 1;         // 2
            const int kLog = 1 << 2;            // 4
            const int kFatal = 1 << 4;          // 16
            const int kException = 1 << 8;      // 256
            const int kWarning = 1 << 9;        // 512

            if ((mode & kException) != 0) return "Exception";
            if ((mode & kFatal) != 0) return "Fatal";
            if ((mode & kError) != 0) return "Error";
            if ((mode & kAssert) != 0) return "Assert";
            if ((mode & kWarning) != 0) return "Warning";
            if ((mode & kLog) != 0) return "Log";

            return "Unknown";
        }

        private static bool IsError(int mode)
        {
            const int kError = 1 << 0;
            const int kFatal = 1 << 4;
            const int kException = 1 << 8;
            return (mode & (kError | kFatal | kException)) != 0;
        }

        private static bool IsException(int mode)
        {
            const int kException = 1 << 8;
            return (mode & kException) != 0;
        }

        private static string ParseFileFromMessage(string message)
        {
            // Try to extract file path from error message
            // Format: "Assets/Scripts/Foo.cs(10,5): error CS1234: ..."
            int parenIndex = message.IndexOf('(');
            if (parenIndex > 0)
            {
                string potential = message.Substring(0, parenIndex).Trim();
                if (potential.EndsWith(".cs"))
                    return potential;
            }
            return null;
        }

        private static int? ParseLineFromMessage(string message)
        {
            // Try to extract line number from error message
            // Format: "Assets/Scripts/Foo.cs(10,5): error CS1234: ..."
            int openParen = message.IndexOf('(');
            int comma = message.IndexOf(',', openParen > 0 ? openParen : 0);

            if (openParen > 0 && comma > openParen)
            {
                string lineStr = message.Substring(openParen + 1, comma - openParen - 1);
                if (int.TryParse(lineStr, out int line))
                    return line;
            }
            return null;
        }

        private static string ParseErrorCode(string message)
        {
            // Extract CS#### error code
            int csIndex = message.IndexOf("CS", StringComparison.OrdinalIgnoreCase);
            if (csIndex >= 0 && csIndex + 6 <= message.Length)
            {
                string potential = message.Substring(csIndex, 6);
                if (potential.Length == 6 && char.IsDigit(potential[2]) && char.IsDigit(potential[3]))
                    return potential.ToUpper();
            }
            return null;
        }

        #endregion
    }
}
