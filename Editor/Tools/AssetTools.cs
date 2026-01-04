using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityAIAssistant.Core.Tools;

namespace UnityAIAssistant.Editor.Tools
{
    /// <summary>
    /// AI tools for asset management operations.
    /// </summary>
    public static class AssetTools
    {
        [AITool("search_assets", "Search for assets in the project by name, type, or label")]
        public static AIToolResult SearchAssets(
            [AIToolParameter("Search filter (name, t:type, l:label)", isOptional: true)] string filter = "",
            [AIToolParameter("Folder to search in (e.g., 'Assets/Scripts')", isOptional: true)] string folder = "Assets",
            [AIToolParameter("Maximum results to return", isOptional: true)] int maxResults = 50)
        {
            try
            {
                string[] searchFolders = { folder };
                var guids = AssetDatabase.FindAssets(filter, searchFolders);

                var assets = guids.Take(maxResults).Select(guid =>
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    return new
                    {
                        name = asset?.name ?? Path.GetFileNameWithoutExtension(path),
                        path,
                        type = asset?.GetType().Name ?? "Unknown",
                        guid
                    };
                }).ToArray();

                return AIToolResult.Succeeded($"Found {assets.Length} assets (of {guids.Length} total)", new { assets });
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Search failed: {ex.Message}");
            }
        }

        [AITool("get_asset_info", "Get detailed information about an asset")]
        public static AIToolResult GetAssetInfo(
            [AIToolParameter("Path to the asset (e.g., 'Assets/Materials/Red.mat')")] string path)
        {
            try
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null)
                {
                    return AIToolResult.Failed($"Asset not found: {path}");
                }

                var guid = AssetDatabase.AssetPathToGUID(path);
                var dependencies = AssetDatabase.GetDependencies(path, false);
                var labels = AssetDatabase.GetLabels(asset);

                var info = new
                {
                    name = asset.name,
                    path,
                    guid,
                    type = asset.GetType().FullName,
                    fileSize = new FileInfo(path).Length,
                    labels,
                    dependencies,
                    isMainAsset = AssetDatabase.IsMainAsset(asset),
                    isNativeAsset = AssetDatabase.IsNativeAsset(asset),
                    isForeignAsset = AssetDatabase.IsForeignAsset(asset)
                };

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get asset info: {ex.Message}");
            }
        }

        [AITool("create_folder", "Create a new folder in the Assets directory")]
        public static AIToolResult CreateFolder(
            [AIToolParameter("Path for the new folder (e.g., 'Assets/NewFolder')")] string path)
        {
            try
            {
                if (AssetDatabase.IsValidFolder(path))
                {
                    return AIToolResult.Failed($"Folder already exists: {path}");
                }

                string parent = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
                string folderName = Path.GetFileName(path);

                string guid = AssetDatabase.CreateFolder(parent, folderName);
                if (string.IsNullOrEmpty(guid))
                {
                    return AIToolResult.Failed($"Failed to create folder: {path}");
                }

                return AIToolResult.Succeeded($"Created folder: {path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to create folder: {ex.Message}");
            }
        }

        [AITool("move_asset", "Move or rename an asset", requiresConfirmation: true)]
        public static AIToolResult MoveAsset(
            [AIToolParameter("Current path of the asset")] string sourcePath,
            [AIToolParameter("New path for the asset")] string destinationPath)
        {
            try
            {
                string error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
                if (!string.IsNullOrEmpty(error))
                {
                    return AIToolResult.Failed($"Failed to move asset: {error}");
                }

                return AIToolResult.Succeeded($"Moved asset from {sourcePath} to {destinationPath}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to move asset: {ex.Message}");
            }
        }

        [AITool("copy_asset", "Copy an asset to a new location")]
        public static AIToolResult CopyAsset(
            [AIToolParameter("Path of the asset to copy")] string sourcePath,
            [AIToolParameter("Destination path for the copy")] string destinationPath)
        {
            try
            {
                bool success = AssetDatabase.CopyAsset(sourcePath, destinationPath);
                if (!success)
                {
                    return AIToolResult.Failed($"Failed to copy asset from {sourcePath} to {destinationPath}");
                }

                return AIToolResult.Succeeded($"Copied asset to {destinationPath}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to copy asset: {ex.Message}");
            }
        }

        [AITool("delete_asset", "Delete an asset", requiresConfirmation: true)]
        public static AIToolResult DeleteAsset(
            [AIToolParameter("Path of the asset to delete")] string path)
        {
            try
            {
                bool success = AssetDatabase.DeleteAsset(path);
                if (!success)
                {
                    return AIToolResult.Failed($"Failed to delete asset: {path}");
                }

                return AIToolResult.Succeeded($"Deleted asset: {path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to delete asset: {ex.Message}");
            }
        }

        [AITool("set_asset_labels", "Set labels on an asset")]
        public static AIToolResult SetAssetLabels(
            [AIToolParameter("Path to the asset")] string path,
            [AIToolParameter("Comma-separated list of labels")] string labels)
        {
            try
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null)
                {
                    return AIToolResult.Failed($"Asset not found: {path}");
                }

                var labelArray = labels.Split(',')
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l))
                    .ToArray();

                AssetDatabase.SetLabels(asset, labelArray);
                return AIToolResult.Succeeded($"Set {labelArray.Length} labels on {path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set labels: {ex.Message}");
            }
        }

        [AITool("import_asset", "Force reimport of an asset")]
        public static AIToolResult ImportAsset(
            [AIToolParameter("Path to the asset to reimport")] string path,
            [AIToolParameter("Import options: 'default', 'force', or 'forceUpdate'", isOptional: true)] string options = "default")
        {
            try
            {
                ImportAssetOptions importOptions = options.ToLower() switch
                {
                    "force" => ImportAssetOptions.ForceUpdate,
                    "forceupdate" => ImportAssetOptions.ForceUpdate,
                    _ => ImportAssetOptions.Default
                };

                AssetDatabase.ImportAsset(path, importOptions);
                return AIToolResult.Succeeded($"Reimported asset: {path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to reimport asset: {ex.Message}");
            }
        }

        [AITool("get_asset_dependencies", "Get all dependencies of an asset")]
        public static AIToolResult GetAssetDependencies(
            [AIToolParameter("Path to the asset")] string path,
            [AIToolParameter("Include recursive dependencies", isOptional: true)] bool recursive = true)
        {
            try
            {
                if (!File.Exists(path) && !AssetDatabase.IsValidFolder(path))
                {
                    return AIToolResult.Failed($"Asset not found: {path}");
                }

                var dependencies = AssetDatabase.GetDependencies(path, recursive);

                var grouped = dependencies
                    .Where(d => d != path)
                    .GroupBy(d => Path.GetExtension(d).ToLower())
                    .Select(g => new { extension = g.Key, paths = g.ToArray() })
                    .ToArray();

                return AIToolResult.Succeeded($"Found {dependencies.Length - 1} dependencies", new { grouped });
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get dependencies: {ex.Message}");
            }
        }

        [AITool("find_references", "Find all assets that reference a given asset")]
        public static AIToolResult FindReferences(
            [AIToolParameter("Path to the asset")] string path)
        {
            try
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                {
                    return AIToolResult.Failed($"Asset not found: {path}");
                }

                // Search all assets for references to this GUID
                var allAssets = AssetDatabase.GetAllAssetPaths();
                var references = new List<string>();

                foreach (var assetPath in allAssets)
                {
                    if (assetPath == path) continue;
                    if (assetPath.EndsWith(".cs")) continue; // Skip scripts for performance

                    var deps = AssetDatabase.GetDependencies(assetPath, false);
                    if (deps.Contains(path))
                    {
                        references.Add(assetPath);
                    }
                }

                return AIToolResult.Succeeded($"Found {references.Count} references", new { references = references.ToArray() });
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to find references: {ex.Message}");
            }
        }

        [AITool("create_scriptable_object", "Create a new ScriptableObject asset", requiresConfirmation: true)]
        public static AIToolResult CreateScriptableObject(
            [AIToolParameter("Full type name of the ScriptableObject")] string typeName,
            [AIToolParameter("Path to save the asset (e.g., 'Assets/Data/MyConfig.asset')")] string path)
        {
            try
            {
                var type = FindType(typeName);
                if (type == null)
                {
                    return AIToolResult.Failed($"Type not found: {typeName}");
                }

                if (!typeof(ScriptableObject).IsAssignableFrom(type))
                {
                    return AIToolResult.Failed($"Type is not a ScriptableObject: {typeName}");
                }

                // Ensure path ends with .asset
                if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".asset";
                }

                // Create directory if needed
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
                {
                    Directory.CreateDirectory(directory);
                    AssetDatabase.Refresh();
                }

                var asset = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();

                return AIToolResult.Succeeded($"Created ScriptableObject at {path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to create ScriptableObject: {ex.Message}");
            }
        }

        [AITool("list_asset_types", "List all available asset types that can be created")]
        public static AIToolResult ListAssetTypes()
        {
            var types = new[]
            {
                new { category = "Materials", types = new[] { "Material", "PhysicMaterial", "PhysicsMaterial2D" } },
                new { category = "Audio", types = new[] { "AudioMixer", "AudioMixerGroup" } },
                new { category = "Rendering", types = new[] { "RenderTexture", "CustomRenderTexture", "LightmapParameters" } },
                new { category = "Animation", types = new[] { "AnimatorController", "AnimatorOverrideController", "AnimationClip", "AvatarMask" } },
                new { category = "UI", types = new[] { "Font", "GUISkin" } },
                new { category = "Terrain", types = new[] { "TerrainLayer" } },
                new { category = "Physics", types = new[] { "PhysicMaterial" } },
                new { category = "Scripting", types = new[] { "ScriptableObject (custom)" } }
            };

            return AIToolResult.Succeeded(JsonConvert.SerializeObject(types, Formatting.Indented));
        }

        private static Type FindType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null) return type;

            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
        }
    }
}
