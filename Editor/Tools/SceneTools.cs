using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityAIAssistant.Core.Tools;

namespace UnityAIAssistant.Editor.Tools
{
    /// <summary>
    /// AI tools for scene management operations.
    /// </summary>
    public static class SceneTools
    {
        [AITool("get_current_scene", "Get information about the currently active scene")]
        public static AIToolResult GetCurrentScene()
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            var info = new
            {
                name = scene.name,
                path = scene.path,
                buildIndex = scene.buildIndex,
                isDirty = scene.isDirty,
                isLoaded = scene.isLoaded,
                rootObjectCount = rootObjects.Length,
                rootObjects = rootObjects.Select(go => new
                {
                    name = go.name,
                    childCount = go.transform.childCount,
                    components = go.GetComponents<Component>()
                        .Where(c => c != null)
                        .Select(c => c.GetType().Name)
                        .ToArray()
                }).ToArray()
            };

            return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
        }

        [AITool("get_scene_hierarchy", "Get the full hierarchy of GameObjects in the current scene")]
        public static AIToolResult GetSceneHierarchy(
            [AIToolParameter("Maximum depth to traverse (default 10)", isOptional: true)] int maxDepth = 10)
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            var hierarchy = rootObjects.Select(go => BuildHierarchyNode(go, 0, maxDepth)).ToArray();

            return AIToolResult.Succeeded(JsonConvert.SerializeObject(new
            {
                sceneName = scene.name,
                hierarchy
            }, Formatting.Indented));
        }

        private static object BuildHierarchyNode(GameObject go, int currentDepth, int maxDepth)
        {
            var children = new List<object>();
            if (currentDepth < maxDepth)
            {
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    children.Add(BuildHierarchyNode(go.transform.GetChild(i).gameObject, currentDepth + 1, maxDepth));
                }
            }

            return new
            {
                name = go.name,
                active = go.activeSelf,
                tag = go.tag,
                layer = LayerMask.LayerToName(go.layer),
                components = go.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name)
                    .ToArray(),
                children = children.Count > 0 ? children : null,
                childCountIfTruncated = currentDepth >= maxDepth && go.transform.childCount > 0
                    ? go.transform.childCount
                    : (int?)null
            };
        }

        [AITool("create_scene", "Create a new scene", requiresConfirmation: true)]
        public static AIToolResult CreateScene(
            [AIToolParameter("Path for the new scene (e.g., 'Assets/Scenes/NewScene.unity')")] string path,
            [AIToolParameter("Setup mode: 'empty', 'default', or 'basic'", isOptional: true)] string setup = "default")
        {
            try
            {
                // Ensure path ends with .unity
                if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".unity";
                }

                // Create directory if needed
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Create the scene based on setup mode
                NewSceneSetup newSceneSetup = setup.ToLower() switch
                {
                    "empty" => NewSceneSetup.EmptyScene,
                    "basic" => NewSceneSetup.DefaultGameObjects,
                    _ => NewSceneSetup.DefaultGameObjects
                };

                var newScene = EditorSceneManager.NewScene(newSceneSetup, NewSceneMode.Single);

                // Save the scene
                bool saved = EditorSceneManager.SaveScene(newScene, path);
                if (!saved)
                {
                    return AIToolResult.Failed($"Failed to save scene at {path}");
                }

                AssetDatabase.Refresh();
                return AIToolResult.Succeeded($"Created scene at {path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to create scene: {ex.Message}");
            }
        }

        [AITool("save_scene", "Save the current scene")]
        public static AIToolResult SaveScene(
            [AIToolParameter("New path to save as (optional, saves to current path if empty)", isOptional: true)] string path = "")
        {
            try
            {
                var scene = SceneManager.GetActiveScene();
                bool saved;

                if (string.IsNullOrEmpty(path))
                {
                    saved = EditorSceneManager.SaveScene(scene);
                }
                else
                {
                    if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    {
                        path += ".unity";
                    }
                    saved = EditorSceneManager.SaveScene(scene, path);
                }

                return saved
                    ? AIToolResult.Succeeded($"Saved scene: {scene.path}")
                    : AIToolResult.Failed("Failed to save scene");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to save scene: {ex.Message}");
            }
        }

        [AITool("load_scene", "Load a scene by path", requiresConfirmation: true)]
        public static AIToolResult LoadScene(
            [AIToolParameter("Path to the scene asset (e.g., 'Assets/Scenes/MainScene.unity')")] string path,
            [AIToolParameter("Load mode: 'single' or 'additive'", isOptional: true)] string mode = "single")
        {
            try
            {
                if (!File.Exists(path))
                {
                    return AIToolResult.Failed($"Scene not found: {path}");
                }

                var openMode = mode.ToLower() == "additive"
                    ? OpenSceneMode.Additive
                    : OpenSceneMode.Single;

                EditorSceneManager.OpenScene(path, openMode);
                return AIToolResult.Succeeded($"Loaded scene: {path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to load scene: {ex.Message}");
            }
        }

        [AITool("find_gameobjects", "Find GameObjects by name, tag, or component type")]
        public static AIToolResult FindGameObjects(
            [AIToolParameter("Name to search for (supports partial match)", isOptional: true)] string name = null,
            [AIToolParameter("Tag to filter by", isOptional: true)] string tag = null,
            [AIToolParameter("Component type name to filter by", isOptional: true)] string componentType = null,
            [AIToolParameter("Include inactive objects", isOptional: true)] bool includeInactive = false)
        {
            try
            {
                IEnumerable<GameObject> results;

                if (!string.IsNullOrEmpty(tag))
                {
                    results = GameObject.FindGameObjectsWithTag(tag);
                }
                else if (!string.IsNullOrEmpty(componentType))
                {
                    var type = FindType(componentType);
                    if (type == null)
                    {
                        return AIToolResult.Failed($"Component type not found: {componentType}");
                    }
                    results = UnityEngine.Object.FindObjectsByType(type,
                        includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                        .Cast<Component>()
                        .Select(c => c.gameObject);
                }
                else
                {
                    // Get all GameObjects
                    var allObjects = includeInactive
                        ? Resources.FindObjectsOfTypeAll<GameObject>().Where(go => go.scene.isLoaded)
                        : UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                    results = allObjects;
                }

                // Filter by name if provided
                if (!string.IsNullOrEmpty(name))
                {
                    results = results.Where(go =>
                        go.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                var found = results.Select(go => new
                {
                    name = go.name,
                    path = GetGameObjectPath(go),
                    tag = go.tag,
                    layer = LayerMask.LayerToName(go.layer),
                    active = go.activeSelf,
                    components = go.GetComponents<Component>()
                        .Where(c => c != null)
                        .Select(c => c.GetType().Name)
                        .ToArray()
                }).Take(50).ToArray();

                return AIToolResult.Succeeded($"Found {found.Length} GameObjects", new { gameObjects = found });
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Search failed: {ex.Message}");
            }
        }

        [AITool("get_scene_lighting", "Get lighting settings for the current scene")]
        public static AIToolResult GetSceneLighting()
        {
            var info = new
            {
                ambientMode = RenderSettings.ambientMode.ToString(),
                ambientColor = ColorToHex(RenderSettings.ambientLight),
                ambientIntensity = RenderSettings.ambientIntensity,
                fogEnabled = RenderSettings.fog,
                fogColor = ColorToHex(RenderSettings.fogColor),
                fogMode = RenderSettings.fogMode.ToString(),
                fogDensity = RenderSettings.fogDensity,
                skybox = RenderSettings.skybox?.name ?? "None",
                sun = RenderSettings.sun?.name ?? "None"
            };

            return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
        }

        [AITool("set_scene_lighting", "Configure scene lighting settings", requiresConfirmation: true)]
        public static AIToolResult SetSceneLighting(
            [AIToolParameter("Ambient color in hex (e.g., '#404040')", isOptional: true)] string ambientColor = null,
            [AIToolParameter("Enable fog", isOptional: true)] bool? fogEnabled = null,
            [AIToolParameter("Fog color in hex", isOptional: true)] string fogColor = null,
            [AIToolParameter("Fog density (0-1)", isOptional: true)] float? fogDensity = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(ambientColor))
                {
                    if (ColorUtility.TryParseHtmlString(ambientColor, out Color color))
                    {
                        RenderSettings.ambientLight = color;
                    }
                }

                if (fogEnabled.HasValue)
                {
                    RenderSettings.fog = fogEnabled.Value;
                }

                if (!string.IsNullOrEmpty(fogColor))
                {
                    if (ColorUtility.TryParseHtmlString(fogColor, out Color color))
                    {
                        RenderSettings.fogColor = color;
                    }
                }

                if (fogDensity.HasValue)
                {
                    RenderSettings.fogDensity = Mathf.Clamp01(fogDensity.Value);
                }

                return AIToolResult.Succeeded("Updated scene lighting settings");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to update lighting: {ex.Message}");
            }
        }

        #region Helpers

        private static string GetGameObjectPath(GameObject go)
        {
            var path = new List<string> { go.name };
            var current = go.transform.parent;
            while (current != null)
            {
                path.Insert(0, current.name);
                current = current.parent;
            }
            return string.Join("/", path);
        }

        private static Type FindType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null) return type;

            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
        }

        private static string ColorToHex(Color color)
        {
            return $"#{ColorUtility.ToHtmlStringRGB(color)}";
        }

        #endregion
    }
}
