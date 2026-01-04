using System;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityAIAssistant.Core.Tools;

namespace UnityAIAssistant.Editor.Tools
{
    /// <summary>
    /// AI tools for prefab operations.
    /// </summary>
    public static class PrefabTools
    {
        [AITool("create_prefab", "Create a prefab from a GameObject in the scene", requiresConfirmation: true)]
        public static AIToolResult CreatePrefab(
            [AIToolParameter("Path or name of the GameObject to make into a prefab")] string gameObjectPath,
            [AIToolParameter("Path to save the prefab (e.g., 'Assets/Prefabs/MyPrefab.prefab')")] string savePath)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                // Ensure path ends with .prefab
                if (!savePath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    savePath += ".prefab";
                }

                // Create directory if needed
                string directory = System.IO.Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                    AssetDatabase.Refresh();
                }

                // Create the prefab
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, savePath);
                if (prefab == null)
                {
                    return AIToolResult.Failed($"Failed to create prefab at {savePath}");
                }

                return AIToolResult.Succeeded($"Created prefab at {savePath}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to create prefab: {ex.Message}");
            }
        }

        [AITool("instantiate_prefab", "Instantiate a prefab in the scene")]
        public static AIToolResult InstantiatePrefab(
            [AIToolParameter("Path to the prefab asset")] string prefabPath,
            [AIToolParameter("Name for the instantiated object (optional)", isOptional: true)] string name = null,
            [AIToolParameter("Parent GameObject path (optional)", isOptional: true)] string parentPath = null,
            [AIToolParameter("Position X", isOptional: true)] float x = 0,
            [AIToolParameter("Position Y", isOptional: true)] float y = 0,
            [AIToolParameter("Position Z", isOptional: true)] float z = 0)
        {
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    return AIToolResult.Failed($"Prefab not found: {prefabPath}");
                }

                Transform parent = null;
                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parentGo = GameObject.Find(parentPath);
                    parent = parentGo?.transform;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.transform.position = new Vector3(x, y, z);

                if (!string.IsNullOrEmpty(name))
                {
                    instance.name = name;
                }

                Undo.RegisterCreatedObjectUndo(instance, $"Instantiate {prefab.name}");
                Selection.activeGameObject = instance;

                return AIToolResult.Succeeded($"Instantiated prefab '{prefab.name}' at ({x}, {y}, {z})");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to instantiate prefab: {ex.Message}");
            }
        }

        [AITool("get_prefab_info", "Get information about a prefab asset or instance")]
        public static AIToolResult GetPrefabInfo(
            [AIToolParameter("Path to prefab asset or name of prefab instance in scene")] string path)
        {
            try
            {
                // Check if it's an asset path
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabAsset != null)
                {
                    var info = new
                    {
                        isAsset = true,
                        name = prefabAsset.name,
                        path,
                        componentCount = prefabAsset.GetComponentsInChildren<Component>(true).Length,
                        childCount = CountChildren(prefabAsset.transform),
                        components = prefabAsset.GetComponents<Component>()
                            .Where(c => c != null)
                            .Select(c => c.GetType().Name)
                            .ToArray()
                    };
                    return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
                }

                // Check if it's a scene object
                var sceneObject = GameObject.Find(path);
                if (sceneObject != null)
                {
                    var prefabStatus = PrefabUtility.GetPrefabInstanceStatus(sceneObject);
                    var prefabType = PrefabUtility.GetPrefabAssetType(sceneObject);
                    var sourceAsset = PrefabUtility.GetCorrespondingObjectFromSource(sceneObject);

                    var info = new
                    {
                        isAsset = false,
                        name = sceneObject.name,
                        isPrefabInstance = prefabStatus != PrefabInstanceStatus.NotAPrefab,
                        prefabStatus = prefabStatus.ToString(),
                        prefabType = prefabType.ToString(),
                        sourcePrefab = sourceAsset != null ? AssetDatabase.GetAssetPath(sourceAsset) : null,
                        hasOverrides = PrefabUtility.HasPrefabInstanceAnyOverrides(sceneObject, false),
                        componentCount = sceneObject.GetComponentsInChildren<Component>(true).Length,
                        childCount = CountChildren(sceneObject.transform)
                    };
                    return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
                }

                return AIToolResult.Failed($"Prefab or GameObject not found: {path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get prefab info: {ex.Message}");
            }
        }

        [AITool("apply_prefab_overrides", "Apply all overrides from a prefab instance to its source prefab", requiresConfirmation: true)]
        public static AIToolResult ApplyPrefabOverrides(
            [AIToolParameter("Path or name of the prefab instance in scene")] string gameObjectPath)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                if (PrefabUtility.GetPrefabInstanceStatus(go) == PrefabInstanceStatus.NotAPrefab)
                {
                    return AIToolResult.Failed($"GameObject is not a prefab instance: {gameObjectPath}");
                }

                var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
                var prefabPath = AssetDatabase.GetAssetPath(sourcePrefab);

                PrefabUtility.ApplyPrefabInstance(go, InteractionMode.UserAction);

                return AIToolResult.Succeeded($"Applied overrides to prefab: {prefabPath}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to apply overrides: {ex.Message}");
            }
        }

        [AITool("revert_prefab_overrides", "Revert all overrides on a prefab instance", requiresConfirmation: true)]
        public static AIToolResult RevertPrefabOverrides(
            [AIToolParameter("Path or name of the prefab instance in scene")] string gameObjectPath)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                if (PrefabUtility.GetPrefabInstanceStatus(go) == PrefabInstanceStatus.NotAPrefab)
                {
                    return AIToolResult.Failed($"GameObject is not a prefab instance: {gameObjectPath}");
                }

                PrefabUtility.RevertPrefabInstance(go, InteractionMode.UserAction);

                return AIToolResult.Succeeded($"Reverted all overrides on {go.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to revert overrides: {ex.Message}");
            }
        }

        [AITool("unpack_prefab", "Unpack a prefab instance", requiresConfirmation: true)]
        public static AIToolResult UnpackPrefab(
            [AIToolParameter("Path or name of the prefab instance in scene")] string gameObjectPath,
            [AIToolParameter("Unpack mode: 'root' or 'completely'", isOptional: true)] string mode = "root")
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                if (PrefabUtility.GetPrefabInstanceStatus(go) == PrefabInstanceStatus.NotAPrefab)
                {
                    return AIToolResult.Failed($"GameObject is not a prefab instance: {gameObjectPath}");
                }

                var unpackMode = mode.ToLower() == "completely"
                    ? PrefabUnpackMode.Completely
                    : PrefabUnpackMode.OutermostRoot;

                PrefabUtility.UnpackPrefabInstance(go, unpackMode, InteractionMode.UserAction);

                return AIToolResult.Succeeded($"Unpacked prefab instance ({mode})");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to unpack prefab: {ex.Message}");
            }
        }

        [AITool("update_prefab", "Update a prefab asset with changes from a scene instance", requiresConfirmation: true)]
        public static AIToolResult UpdatePrefab(
            [AIToolParameter("Path or name of the modified prefab instance in scene")] string gameObjectPath,
            [AIToolParameter("Path to save the updated prefab (uses original if empty)", isOptional: true)] string savePath = "")
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                string targetPath = savePath;
                if (string.IsNullOrEmpty(targetPath))
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    if (source != null)
                    {
                        targetPath = AssetDatabase.GetAssetPath(source);
                    }
                }

                if (string.IsNullOrEmpty(targetPath))
                {
                    return AIToolResult.Failed("No save path specified and object is not a prefab instance");
                }

                PrefabUtility.SaveAsPrefabAsset(go, targetPath);
                return AIToolResult.Succeeded($"Updated prefab at {targetPath}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to update prefab: {ex.Message}");
            }
        }

        [AITool("list_prefabs", "List all prefabs in a folder")]
        public static AIToolResult ListPrefabs(
            [AIToolParameter("Folder to search (e.g., 'Assets/Prefabs')", isOptional: true)] string folder = "Assets")
        {
            try
            {
                var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
                var prefabs = guids.Select(guid =>
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    return new
                    {
                        name = prefab?.name ?? System.IO.Path.GetFileNameWithoutExtension(path),
                        path,
                        componentCount = prefab?.GetComponents<Component>().Length ?? 0
                    };
                }).ToArray();

                return AIToolResult.Succeeded($"Found {prefabs.Length} prefabs", new { prefabs });
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to list prefabs: {ex.Message}");
            }
        }

        private static int CountChildren(Transform transform)
        {
            int count = transform.childCount;
            for (int i = 0; i < transform.childCount; i++)
            {
                count += CountChildren(transform.GetChild(i));
            }
            return count;
        }
    }
}
