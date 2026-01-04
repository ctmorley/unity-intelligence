using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityAIAssistant.Core.Tools;

namespace UnityAIAssistant.Editor.Tools
{
    /// <summary>
    /// Built-in AI tools for common Unity Editor operations.
    /// </summary>
    public static class EditorTools
    {
        #region Script Management

        [AITool("create_script", "Create a new C# script file in the Unity project")]
        public static AIToolResult CreateScript(
            [AIToolParameter("Path relative to Assets folder (e.g., 'Scripts/MyScript.cs')")] string path,
            [AIToolParameter("Complete C# source code for the script")] string content)
        {
            try
            {
                // Ensure path ends with .cs
                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".cs";
                }

                // Build full path
                string fullPath = Path.Combine(Application.dataPath, path);
                string directory = Path.GetDirectoryName(fullPath);

                // Create directory if needed
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Check if file exists
                if (File.Exists(fullPath))
                {
                    return AIToolResult.Failed($"Script already exists at Assets/{path}. Use modify_script to update it.");
                }

                // Write the file
                File.WriteAllText(fullPath, content);
                AssetDatabase.Refresh();

                return AIToolResult.Succeeded($"Created script at Assets/{path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to create script: {ex.Message}");
            }
        }

        [AITool("modify_script", "Modify an existing C# script file", requiresConfirmation: true)]
        public static AIToolResult ModifyScript(
            [AIToolParameter("Path relative to Assets folder")] string path,
            [AIToolParameter("New complete content for the script")] string content)
        {
            try
            {
                string fullPath = Path.Combine(Application.dataPath, path);

                if (!File.Exists(fullPath))
                {
                    return AIToolResult.Failed($"Script not found at Assets/{path}");
                }

                File.WriteAllText(fullPath, content);
                AssetDatabase.Refresh();

                return AIToolResult.Succeeded($"Modified script at Assets/{path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to modify script: {ex.Message}");
            }
        }

        [AITool("read_script", "Read the contents of a C# script file")]
        public static AIToolResult ReadScript(
            [AIToolParameter("Path relative to Assets folder")] string path)
        {
            try
            {
                string fullPath = Path.Combine(Application.dataPath, path);

                if (!File.Exists(fullPath))
                {
                    return AIToolResult.Failed($"Script not found at Assets/{path}");
                }

                string content = File.ReadAllText(fullPath);
                return AIToolResult.Succeeded(content);
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to read script: {ex.Message}");
            }
        }

        [AITool("list_scripts", "List all C# scripts in a directory")]
        public static AIToolResult ListScripts(
            [AIToolParameter("Directory relative to Assets (empty for root)", isOptional: true)] string directory = "")
        {
            try
            {
                string basePath = string.IsNullOrEmpty(directory)
                    ? Application.dataPath
                    : Path.Combine(Application.dataPath, directory);

                if (!Directory.Exists(basePath))
                {
                    return AIToolResult.Failed($"Directory not found: Assets/{directory}");
                }

                var scripts = Directory.GetFiles(basePath, "*.cs", SearchOption.AllDirectories)
                    .Select(p => "Assets/" + p.Replace(Application.dataPath + Path.DirectorySeparatorChar, "")
                        .Replace(Path.DirectorySeparatorChar, '/'))
                    .OrderBy(p => p)
                    .ToArray();

                return AIToolResult.Succeeded(
                    $"Found {scripts.Length} scripts",
                    new { scripts }
                );
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to list scripts: {ex.Message}");
            }
        }

        [AITool("edit_script", "Edit a specific portion of a script (find and replace)", requiresConfirmation: true)]
        public static AIToolResult EditScript(
            [AIToolParameter("Path relative to Assets folder")] string path,
            [AIToolParameter("Text to find in the script")] string findText,
            [AIToolParameter("Text to replace with")] string replaceText,
            [AIToolParameter("Replace all occurrences (default: first only)", isOptional: true)] bool replaceAll = false)
        {
            try
            {
                string fullPath = Path.Combine(Application.dataPath, path);

                if (!File.Exists(fullPath))
                {
                    return AIToolResult.Failed($"Script not found at Assets/{path}");
                }

                string content = File.ReadAllText(fullPath);

                if (!content.Contains(findText))
                {
                    return AIToolResult.Failed("Text to find not found in script");
                }

                string newContent;
                int count;
                if (replaceAll)
                {
                    count = (content.Length - content.Replace(findText, "").Length) / findText.Length;
                    newContent = content.Replace(findText, replaceText);
                }
                else
                {
                    int index = content.IndexOf(findText, StringComparison.Ordinal);
                    newContent = content.Substring(0, index) + replaceText + content.Substring(index + findText.Length);
                    count = 1;
                }

                File.WriteAllText(fullPath, newContent);
                AssetDatabase.Refresh();

                return AIToolResult.Succeeded($"Replaced {count} occurrence(s) in Assets/{path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to edit script: {ex.Message}");
            }
        }

        [AITool("insert_in_script", "Insert code at a specific line in a script", requiresConfirmation: true)]
        public static AIToolResult InsertInScript(
            [AIToolParameter("Path relative to Assets folder")] string path,
            [AIToolParameter("Line number to insert at (1-based, inserts before this line)")] int lineNumber,
            [AIToolParameter("Code to insert")] string codeToInsert)
        {
            try
            {
                string fullPath = Path.Combine(Application.dataPath, path);

                if (!File.Exists(fullPath))
                {
                    return AIToolResult.Failed($"Script not found at Assets/{path}");
                }

                var lines = File.ReadAllLines(fullPath).ToList();

                if (lineNumber < 1 || lineNumber > lines.Count + 1)
                {
                    return AIToolResult.Failed($"Line number {lineNumber} out of range (1-{lines.Count + 1})");
                }

                // Split inserted code into lines
                var insertLines = codeToInsert.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                lines.InsertRange(lineNumber - 1, insertLines);

                File.WriteAllLines(fullPath, lines);
                AssetDatabase.Refresh();

                return AIToolResult.Succeeded($"Inserted {insertLines.Length} line(s) at line {lineNumber} in Assets/{path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to insert in script: {ex.Message}");
            }
        }

        [AITool("append_to_script", "Append code to the end of a script", requiresConfirmation: true)]
        public static AIToolResult AppendToScript(
            [AIToolParameter("Path relative to Assets folder")] string path,
            [AIToolParameter("Code to append")] string codeToAppend)
        {
            try
            {
                string fullPath = Path.Combine(Application.dataPath, path);

                if (!File.Exists(fullPath))
                {
                    return AIToolResult.Failed($"Script not found at Assets/{path}");
                }

                File.AppendAllText(fullPath, "\n" + codeToAppend);
                AssetDatabase.Refresh();

                return AIToolResult.Succeeded($"Appended code to Assets/{path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to append to script: {ex.Message}");
            }
        }

        [AITool("delete_script_lines", "Delete specific lines from a script", requiresConfirmation: true)]
        public static AIToolResult DeleteScriptLines(
            [AIToolParameter("Path relative to Assets folder")] string path,
            [AIToolParameter("Starting line number (1-based)")] int startLine,
            [AIToolParameter("Ending line number (inclusive)")] int endLine)
        {
            try
            {
                string fullPath = Path.Combine(Application.dataPath, path);

                if (!File.Exists(fullPath))
                {
                    return AIToolResult.Failed($"Script not found at Assets/{path}");
                }

                var lines = File.ReadAllLines(fullPath).ToList();

                if (startLine < 1 || endLine > lines.Count || startLine > endLine)
                {
                    return AIToolResult.Failed($"Invalid line range {startLine}-{endLine} (file has {lines.Count} lines)");
                }

                int count = endLine - startLine + 1;
                lines.RemoveRange(startLine - 1, count);

                File.WriteAllLines(fullPath, lines);
                AssetDatabase.Refresh();

                return AIToolResult.Succeeded($"Deleted lines {startLine}-{endLine} ({count} lines) from Assets/{path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to delete lines: {ex.Message}");
            }
        }

        [AITool("analyze_script", "Analyze a script's structure (classes, methods, fields)")]
        public static AIToolResult AnalyzeScript(
            [AIToolParameter("Path relative to Assets folder")] string path)
        {
            try
            {
                string fullPath = Path.Combine(Application.dataPath, path);

                if (!File.Exists(fullPath))
                {
                    return AIToolResult.Failed($"Script not found at Assets/{path}");
                }

                string content = File.ReadAllText(fullPath);
                var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

                // Simple regex-based analysis
                var namespaces = System.Text.RegularExpressions.Regex.Matches(content, @"namespace\s+([\w.]+)")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => m.Groups[1].Value)
                    .ToArray();

                var classes = System.Text.RegularExpressions.Regex.Matches(content, @"(public|private|internal|protected)?\s*(abstract|sealed|static)?\s*class\s+(\w+)")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => new { modifier = m.Groups[1].Value, keyword = m.Groups[2].Value, name = m.Groups[3].Value })
                    .ToArray();

                var methods = System.Text.RegularExpressions.Regex.Matches(content, @"(public|private|internal|protected)\s*(static|virtual|override|abstract|async)?\s*(\w+(?:<[\w,\s]+>)?)\s+(\w+)\s*\(")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => new { modifier = m.Groups[1].Value, keyword = m.Groups[2].Value, returnType = m.Groups[3].Value, name = m.Groups[4].Value })
                    .Where(m => m.returnType != "class" && m.returnType != "interface" && m.returnType != "struct")
                    .ToArray();

                var usings = System.Text.RegularExpressions.Regex.Matches(content, @"using\s+([\w.]+);")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => m.Groups[1].Value)
                    .ToArray();

                var info = new
                {
                    path = $"Assets/{path}",
                    lineCount = lines.Length,
                    usings,
                    namespaces,
                    classes,
                    methodCount = methods.Length,
                    methods = methods.Take(20).ToArray() // Limit to first 20
                };

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to analyze script: {ex.Message}");
            }
        }

        [AITool("create_script_from_template", "Create a script from a built-in template")]
        public static AIToolResult CreateScriptFromTemplate(
            [AIToolParameter("Path relative to Assets folder")] string path,
            [AIToolParameter("Class name")] string className,
            [AIToolParameter("Template: 'monobehaviour', 'scriptableobject', 'editor', 'editorwindow', 'propertyDrawer', 'interface', 'static'")] string template,
            [AIToolParameter("Namespace (optional)", isOptional: true)] string namespaceName = "")
        {
            try
            {
                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".cs";
                }

                string fullPath = Path.Combine(Application.dataPath, path);

                if (File.Exists(fullPath))
                {
                    return AIToolResult.Failed($"Script already exists at Assets/{path}");
                }

                string content = GetScriptTemplate(template, className, namespaceName);
                if (string.IsNullOrEmpty(content))
                {
                    return AIToolResult.Failed($"Unknown template: {template}");
                }

                string directory = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(fullPath, content);
                AssetDatabase.Refresh();

                return AIToolResult.Succeeded($"Created {template} script at Assets/{path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to create script: {ex.Message}");
            }
        }

        private static string GetScriptTemplate(string template, string className, string namespaceName)
        {
            string content = template.ToLower() switch
            {
                "monobehaviour" => $@"using UnityEngine;

public class {className} : MonoBehaviour
{{
    void Start()
    {{

    }}

    void Update()
    {{

    }}
}}",

                "scriptableobject" => $@"using UnityEngine;

[CreateAssetMenu(fileName = ""{className}"", menuName = ""ScriptableObjects/{className}"")]
public class {className} : ScriptableObject
{{

}}",

                "editor" => $@"using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TargetComponent))]
public class {className} : Editor
{{
    public override void OnInspectorGUI()
    {{
        base.OnInspectorGUI();

        // Custom inspector code here
    }}
}}",

                "editorwindow" => $@"using UnityEngine;
using UnityEditor;

public class {className} : EditorWindow
{{
    [MenuItem(""Window/{className}"")]
    public static void ShowWindow()
    {{
        GetWindow<{className}>(""{className}"");
    }}

    void OnGUI()
    {{
        // Window GUI code here
    }}
}}",

                "propertydrawer" => $@"using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(TargetAttribute))]
public class {className} : PropertyDrawer
{{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {{
        EditorGUI.PropertyField(position, property, label);
    }}

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {{
        return EditorGUIUtility.singleLineHeight;
    }}
}}",

                "interface" => $@"public interface {className}
{{

}}",

                "static" => $@"using UnityEngine;

public static class {className}
{{

}}",

                _ => null
            };

            if (content == null) return null;

            // Wrap in namespace if provided
            if (!string.IsNullOrEmpty(namespaceName))
            {
                var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                var indented = string.Join("\n    ", lines);
                content = $@"namespace {namespaceName}
{{
    {indented}
}}";
            }

            return content;
        }

        #endregion

        #region GameObject Operations

        [AITool("get_selected_object", "Get information about the currently selected GameObject")]
        public static AIToolResult GetSelectedObject()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                return AIToolResult.Failed("No GameObject currently selected");
            }

            var info = new
            {
                name = selected.name,
                path = GetGameObjectPath(selected),
                tag = selected.tag,
                layer = LayerMask.LayerToName(selected.layer),
                isActive = selected.activeSelf,
                isStatic = selected.isStatic,
                components = selected.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name)
                    .ToArray(),
                childCount = selected.transform.childCount,
                position = new { x = selected.transform.position.x, y = selected.transform.position.y, z = selected.transform.position.z },
                rotation = new { x = selected.transform.eulerAngles.x, y = selected.transform.eulerAngles.y, z = selected.transform.eulerAngles.z },
                scale = new { x = selected.transform.localScale.x, y = selected.transform.localScale.y, z = selected.transform.localScale.z }
            };

            return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
        }

        [AITool("create_gameobject", "Create a new GameObject in the scene", requiresConfirmation: true)]
        public static AIToolResult CreateGameObject(
            [AIToolParameter("Name for the new GameObject")] string name,
            [AIToolParameter("Parent GameObject path (optional)", isOptional: true)] string parentPath = null)
        {
            try
            {
                var newObject = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(newObject, $"Create {name}");

                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parent = GameObject.Find(parentPath);
                    if (parent != null)
                    {
                        newObject.transform.SetParent(parent.transform, false);
                    }
                }

                Selection.activeGameObject = newObject;
                return AIToolResult.Succeeded($"Created GameObject '{name}'");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to create GameObject: {ex.Message}");
            }
        }

        [AITool("add_component", "Add a component to a GameObject", requiresConfirmation: true)]
        public static AIToolResult AddComponent(
            [AIToolParameter("Path or name of the target GameObject")] string gameObjectPath,
            [AIToolParameter("Full type name of the component (e.g., 'UnityEngine.Rigidbody')")] string componentType)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                // Find the type
                Type type = Type.GetType(componentType);
                if (type == null)
                {
                    // Try to find in all assemblies
                    type = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a =>
                        {
                            try { return a.GetTypes(); }
                            catch { return Array.Empty<Type>(); }
                        })
                        .FirstOrDefault(t => t.FullName == componentType || t.Name == componentType);
                }

                if (type == null)
                {
                    return AIToolResult.Failed($"Component type not found: {componentType}");
                }

                if (!typeof(Component).IsAssignableFrom(type))
                {
                    return AIToolResult.Failed($"Type is not a Component: {componentType}");
                }

                Undo.AddComponent(go, type);
                return AIToolResult.Succeeded($"Added {type.Name} to {go.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to add component: {ex.Message}");
            }
        }

        #endregion

        #region Project Info

        [AITool("get_project_info", "Get information about the current Unity project")]
        public static AIToolResult GetProjectInfo()
        {
            var info = new
            {
                projectName = Application.productName,
                companyName = Application.companyName,
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                dataPath = Application.dataPath,
                isPlaying = Application.isPlaying,
                scriptingBackend = PlayerSettings.GetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup).ToString(),
                apiCompatibility = PlayerSettings.GetApiCompatibilityLevel(EditorUserBuildSettings.selectedBuildTargetGroup).ToString()
            };

            return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
        }

        [AITool("get_console_errors", "Get recent error messages from the Unity console")]
        public static AIToolResult GetConsoleErrors()
        {
            // Note: Unity doesn't expose console logs directly via API
            // This is a simplified version - full implementation would need reflection
            return AIToolResult.Succeeded(
                "Console access requires Unity's internal APIs. " +
                "Check the Console window for errors, or use Debug.Log in your scripts."
            );
        }

        #endregion

        #region Helpers

        private static string GetGameObjectPath(GameObject obj)
        {
            var path = new System.Collections.Generic.List<string> { obj.name };
            var current = obj.transform.parent;
            while (current != null)
            {
                path.Insert(0, current.name);
                current = current.parent;
            }
            return string.Join("/", path);
        }

        #endregion
    }
}
