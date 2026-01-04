using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityAIAssistant.Core.Tools;

namespace UnityAIAssistant.Editor.Tools
{
    /// <summary>
    /// AI tools for Unity Editor utilities and operations.
    /// </summary>
    public static class EditorUtilityTools
    {
        #region Selection

        [AITool("get_selection", "Get the currently selected objects in the editor")]
        public static AIToolResult GetSelection()
        {
            var selection = Selection.objects;
            var activeObject = Selection.activeObject;
            var activeGameObject = Selection.activeGameObject;

            var selectedObjects = selection.Select(obj => new
            {
                name = obj.name,
                type = obj.GetType().Name,
                path = AssetDatabase.GetAssetPath(obj),
                isAsset = !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(obj))
            }).ToArray();

            var info = new
            {
                count = selection.Length,
                activeObject = activeObject?.name,
                activeObjectType = activeObject?.GetType().Name,
                activeGameObject = activeGameObject?.name,
                selectedObjects
            };

            return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
        }

        [AITool("set_selection", "Set the editor selection to specific objects")]
        public static AIToolResult SetSelection(
            [AIToolParameter("Comma-separated list of GameObject names or asset paths")] string targets)
        {
            try
            {
                var targetList = targets.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToArray();
                var selectedObjects = new List<UnityEngine.Object>();

                foreach (var target in targetList)
                {
                    // Try as asset path first
                    var asset = AssetDatabase.LoadMainAssetAtPath(target);
                    if (asset != null)
                    {
                        selectedObjects.Add(asset);
                        continue;
                    }

                    // Try as GameObject name/path
                    var go = GameObject.Find(target);
                    if (go != null)
                    {
                        selectedObjects.Add(go);
                    }
                }

                if (selectedObjects.Count == 0)
                {
                    return AIToolResult.Failed("No valid objects found to select");
                }

                Selection.objects = selectedObjects.ToArray();
                return AIToolResult.Succeeded($"Selected {selectedObjects.Count} objects");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set selection: {ex.Message}");
            }
        }

        [AITool("clear_selection", "Clear the current selection")]
        public static AIToolResult ClearSelection()
        {
            Selection.activeObject = null;
            Selection.objects = Array.Empty<UnityEngine.Object>();
            return AIToolResult.Succeeded("Selection cleared");
        }

        [AITool("select_all_of_type", "Select all objects of a specific type in the scene")]
        public static AIToolResult SelectAllOfType(
            [AIToolParameter("Component type name (e.g., 'Camera', 'Light', 'AudioSource')")] string typeName)
        {
            try
            {
                var type = FindType(typeName);
                if (type == null)
                {
                    return AIToolResult.Failed($"Type not found: {typeName}");
                }

                var objects = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None);
                if (objects.Length == 0)
                {
                    return AIToolResult.Failed($"No objects of type {typeName} found in scene");
                }

                // Convert to GameObjects if they're Components
                var gameObjects = objects.Select(o =>
                {
                    if (o is Component c) return c.gameObject;
                    if (o is GameObject go) return go;
                    return null;
                }).Where(go => go != null).Distinct().ToArray();

                Selection.objects = gameObjects;
                return AIToolResult.Succeeded($"Selected {gameObjects.Length} objects of type {typeName}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to select: {ex.Message}");
            }
        }

        #endregion

        #region Undo/Redo

        [AITool("undo", "Undo the last action")]
        public static AIToolResult UndoAction()
        {
            UnityEditor.Undo.PerformUndo();
            return AIToolResult.Succeeded("Undo performed");
        }

        [AITool("redo", "Redo the last undone action")]
        public static AIToolResult RedoAction()
        {
            UnityEditor.Undo.PerformRedo();
            return AIToolResult.Succeeded("Redo performed");
        }

        [AITool("get_undo_history", "Get the undo history")]
        public static AIToolResult GetUndoHistory()
        {
            // Unity doesn't expose full undo history, but we can get current group
            var currentGroup = UnityEditor.Undo.GetCurrentGroupName();

            return AIToolResult.Succeeded($"Current undo group: {currentGroup}");
        }

        #endregion

        #region Play Mode

        [AITool("get_play_mode_state", "Get the current play mode state")]
        public static AIToolResult GetPlayModeState()
        {
            var info = new
            {
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                isPlayingOrWillChangePlaymode = EditorApplication.isPlayingOrWillChangePlaymode,
                isCompiling = EditorApplication.isCompiling
            };

            return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
        }

        [AITool("enter_play_mode", "Enter play mode")]
        public static AIToolResult EnterPlayMode()
        {
            if (EditorApplication.isPlaying)
            {
                return AIToolResult.Failed("Already in play mode");
            }

            EditorApplication.isPlaying = true;
            return AIToolResult.Succeeded("Entering play mode");
        }

        [AITool("exit_play_mode", "Exit play mode")]
        public static AIToolResult ExitPlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                return AIToolResult.Failed("Not in play mode");
            }

            EditorApplication.isPlaying = false;
            return AIToolResult.Succeeded("Exiting play mode");
        }

        [AITool("pause_play_mode", "Pause or unpause play mode")]
        public static AIToolResult PausePlayMode(
            [AIToolParameter("Pause state")] bool paused)
        {
            if (!EditorApplication.isPlaying)
            {
                return AIToolResult.Failed("Not in play mode");
            }

            EditorApplication.isPaused = paused;
            return AIToolResult.Succeeded($"Play mode {(paused ? "paused" : "resumed")}");
        }

        [AITool("step_frame", "Step one frame in paused play mode")]
        public static AIToolResult StepFrame()
        {
            if (!EditorApplication.isPlaying)
            {
                return AIToolResult.Failed("Not in play mode");
            }

            EditorApplication.Step();
            return AIToolResult.Succeeded("Stepped one frame");
        }

        #endregion

        #region Console

        [AITool("log_message", "Log a message to the Unity console")]
        public static AIToolResult LogMessage(
            [AIToolParameter("Message to log")] string message,
            [AIToolParameter("Log type: 'info', 'warning', or 'error'", isOptional: true)] string logType = "info")
        {
            switch (logType.ToLower())
            {
                case "warning":
                    Debug.LogWarning($"[AI Assistant] {message}");
                    break;
                case "error":
                    Debug.LogError($"[AI Assistant] {message}");
                    break;
                default:
                    Debug.Log($"[AI Assistant] {message}");
                    break;
            }

            return AIToolResult.Succeeded($"Logged {logType} message");
        }

        [AITool("clear_console", "Clear the Unity console")]
        public static AIToolResult ClearConsole()
        {
            try
            {
                var logEntries = Type.GetType("UnityEditor.LogEntries, UnityEditor");
                var clearMethod = logEntries?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
                clearMethod?.Invoke(null, null);
                return AIToolResult.Succeeded("Console cleared");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to clear console: {ex.Message}");
            }
        }

        #endregion

        #region Menu Commands

        [AITool("execute_menu_item", "Execute a Unity menu item")]
        public static AIToolResult ExecuteMenuItem(
            [AIToolParameter("Menu item path (e.g., 'Edit/Preferences...', 'GameObject/3D Object/Cube')")] string menuPath)
        {
            try
            {
                bool success = EditorApplication.ExecuteMenuItem(menuPath);
                if (success)
                {
                    return AIToolResult.Succeeded($"Executed menu item: {menuPath}");
                }
                else
                {
                    return AIToolResult.Failed($"Menu item not found or couldn't be executed: {menuPath}");
                }
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to execute menu item: {ex.Message}");
            }
        }

        [AITool("list_menu_items", "List common menu items by category")]
        public static AIToolResult ListMenuItems(
            [AIToolParameter("Category: 'create', 'edit', 'assets', 'window', 'tools'", isOptional: true)] string category = "")
        {
            var menuItems = new Dictionary<string, string[]>
            {
                ["create"] = new[]
                {
                    "GameObject/Create Empty",
                    "GameObject/Create Empty Child",
                    "GameObject/3D Object/Cube",
                    "GameObject/3D Object/Sphere",
                    "GameObject/3D Object/Capsule",
                    "GameObject/3D Object/Cylinder",
                    "GameObject/3D Object/Plane",
                    "GameObject/3D Object/Quad",
                    "GameObject/2D Object/Sprite",
                    "GameObject/Light/Directional Light",
                    "GameObject/Light/Point Light",
                    "GameObject/Light/Spotlight",
                    "GameObject/Camera",
                    "GameObject/UI/Canvas",
                    "GameObject/UI/Button",
                    "GameObject/UI/Text",
                    "GameObject/UI/Image",
                    "GameObject/UI/Panel"
                },
                ["edit"] = new[]
                {
                    "Edit/Undo",
                    "Edit/Redo",
                    "Edit/Cut",
                    "Edit/Copy",
                    "Edit/Paste",
                    "Edit/Duplicate",
                    "Edit/Delete",
                    "Edit/Select All",
                    "Edit/Deselect All",
                    "Edit/Play",
                    "Edit/Pause",
                    "Edit/Step"
                },
                ["assets"] = new[]
                {
                    "Assets/Create/Folder",
                    "Assets/Create/C# Script",
                    "Assets/Create/Material",
                    "Assets/Create/Prefab",
                    "Assets/Create/Scene",
                    "Assets/Refresh",
                    "Assets/Reimport",
                    "Assets/Reimport All"
                },
                ["window"] = new[]
                {
                    "Window/General/Scene",
                    "Window/General/Game",
                    "Window/General/Inspector",
                    "Window/General/Hierarchy",
                    "Window/General/Project",
                    "Window/General/Console",
                    "Window/Animation/Animation",
                    "Window/Animation/Animator",
                    "Window/Rendering/Lighting"
                },
                ["tools"] = new[]
                {
                    "Tools/Transform Tools/Reset",
                    "Tools/Transform Tools/Align/X",
                    "Tools/Transform Tools/Align/Y",
                    "Tools/Transform Tools/Align/Z"
                }
            };

            if (!string.IsNullOrEmpty(category))
            {
                var key = menuItems.Keys.FirstOrDefault(k =>
                    k.Equals(category, StringComparison.OrdinalIgnoreCase));
                if (key != null)
                {
                    return AIToolResult.Succeeded(JsonConvert.SerializeObject(new { category = key, items = menuItems[key] }, Formatting.Indented));
                }
                return AIToolResult.Failed($"Category not found: {category}. Available: {string.Join(", ", menuItems.Keys)}");
            }

            return AIToolResult.Succeeded(JsonConvert.SerializeObject(menuItems, Formatting.Indented));
        }

        #endregion

        #region Project Info

        [AITool("get_project_info", "Get information about the current Unity project")]
        public static AIToolResult GetProjectInfo()
        {
            var info = new
            {
                projectPath = Application.dataPath.Replace("/Assets", ""),
                assetsPath = Application.dataPath,
                productName = Application.productName,
                companyName = Application.companyName,
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                isPlaying = EditorApplication.isPlaying,
                isCompiling = EditorApplication.isCompiling,
                scriptingBackend = PlayerSettings.GetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup).ToString()
            };

            return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
        }

        [AITool("get_project_settings", "Get project settings")]
        public static AIToolResult GetProjectSettings(
            [AIToolParameter("Settings category: 'player', 'quality', 'physics', 'time', 'audio', 'graphics'", isOptional: true)] string category = "player")
        {
            try
            {
                object settings = category.ToLower() switch
                {
                    "player" => new
                    {
                        productName = PlayerSettings.productName,
                        companyName = PlayerSettings.companyName,
                        bundleVersion = PlayerSettings.bundleVersion,
                        defaultIcon = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Unknown).FirstOrDefault()?.name ?? "None",
                        apiCompatibilityLevel = PlayerSettings.GetApiCompatibilityLevel(EditorUserBuildSettings.selectedBuildTargetGroup).ToString()
                    },
                    "quality" => new
                    {
                        currentLevel = QualitySettings.GetQualityLevel(),
                        levelNames = QualitySettings.names,
                        pixelLightCount = QualitySettings.pixelLightCount,
                        antiAliasing = QualitySettings.antiAliasing,
                        shadowDistance = QualitySettings.shadowDistance,
                        vSyncCount = QualitySettings.vSyncCount
                    },
                    "physics" => new
                    {
                        gravity = new { x = Physics.gravity.x, y = Physics.gravity.y, z = Physics.gravity.z },
                        defaultContactOffset = Physics.defaultContactOffset,
                        bounceThreshold = Physics.bounceThreshold,
                        defaultSolverIterations = Physics.defaultSolverIterations
                    },
                    "time" => new
                    {
                        fixedDeltaTime = Time.fixedDeltaTime,
                        maximumDeltaTime = Time.maximumDeltaTime,
                        timeScale = Time.timeScale,
                        maximumParticleDeltaTime = Time.maximumParticleDeltaTime
                    },
                    "audio" => new
                    {
                        speakerMode = AudioSettings.speakerMode.ToString(),
                        sampleRate = AudioSettings.outputSampleRate,
                        dspBufferSize = AudioSettings.GetConfiguration().dspBufferSize
                    },
                    "graphics" => new
                    {
                        colorSpace = QualitySettings.activeColorSpace.ToString(),
                        renderPipelineAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name ?? "Built-in"
                    },
                    _ => null
                };

                if (settings == null)
                {
                    return AIToolResult.Failed($"Unknown category: {category}. Available: player, quality, physics, time, audio, graphics");
                }

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(settings, Formatting.Indented));
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get settings: {ex.Message}");
            }
        }

        #endregion

        #region Build

        [AITool("get_build_settings", "Get build settings information")]
        public static AIToolResult GetBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Select(s => new
            {
                path = s.path,
                enabled = s.enabled,
                guid = s.guid.ToString()
            }).ToArray();

            var info = new
            {
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                selectedBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup.ToString(),
                development = EditorUserBuildSettings.development,
                scenes
            };

            return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
        }

        [AITool("set_build_scenes", "Set scenes in build settings", requiresConfirmation: true)]
        public static AIToolResult SetBuildScenes(
            [AIToolParameter("Comma-separated list of scene paths (e.g., 'Assets/Scenes/Main.unity,Assets/Scenes/Level1.unity')")] string scenePaths)
        {
            try
            {
                var paths = scenePaths.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToArray();
                var scenes = paths.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();

                EditorBuildSettings.scenes = scenes;
                return AIToolResult.Succeeded($"Set {scenes.Length} scenes in build settings");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set build scenes: {ex.Message}");
            }
        }

        #endregion

        #region Editor Preferences

        [AITool("get_editor_pref", "Get an editor preference value")]
        public static AIToolResult GetEditorPref(
            [AIToolParameter("Preference key")] string key,
            [AIToolParameter("Value type: 'int', 'float', 'string', 'bool'", isOptional: true)] string type = "string")
        {
            try
            {
                object value = type.ToLower() switch
                {
                    "int" => EditorPrefs.GetInt(key),
                    "float" => EditorPrefs.GetFloat(key),
                    "bool" => EditorPrefs.GetBool(key),
                    _ => EditorPrefs.GetString(key)
                };

                return AIToolResult.Succeeded($"{key} = {value}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get pref: {ex.Message}");
            }
        }

        [AITool("set_editor_pref", "Set an editor preference value", requiresConfirmation: true)]
        public static AIToolResult SetEditorPref(
            [AIToolParameter("Preference key")] string key,
            [AIToolParameter("Value")] string value,
            [AIToolParameter("Value type: 'int', 'float', 'string', 'bool'", isOptional: true)] string type = "string")
        {
            try
            {
                switch (type.ToLower())
                {
                    case "int":
                        EditorPrefs.SetInt(key, int.Parse(value));
                        break;
                    case "float":
                        EditorPrefs.SetFloat(key, float.Parse(value));
                        break;
                    case "bool":
                        EditorPrefs.SetBool(key, bool.Parse(value));
                        break;
                    default:
                        EditorPrefs.SetString(key, value);
                        break;
                }

                return AIToolResult.Succeeded($"Set {key} = {value}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set pref: {ex.Message}");
            }
        }

        #endregion

        #region Refresh/Compile

        [AITool("refresh_assets", "Refresh the asset database")]
        public static AIToolResult RefreshAssets(
            [AIToolParameter("Import options: 'default', 'force'", isOptional: true)] string options = "default")
        {
            var importOptions = options.ToLower() == "force"
                ? ImportAssetOptions.ForceUpdate
                : ImportAssetOptions.Default;

            AssetDatabase.Refresh(importOptions);
            return AIToolResult.Succeeded("Asset database refreshed");
        }

        [AITool("compile_scripts", "Force recompilation of scripts")]
        public static AIToolResult CompileScripts()
        {
            AssetDatabase.Refresh();
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            return AIToolResult.Succeeded("Script compilation requested");
        }

        [AITool("get_compilation_status", "Check if scripts are currently compiling")]
        public static AIToolResult GetCompilationStatus()
        {
            var info = new
            {
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating
            };

            return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
        }

        #endregion

        #region Helpers

        private static Type FindType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null) return type;

            type = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null) return type;

            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
        }

        #endregion
    }
}
