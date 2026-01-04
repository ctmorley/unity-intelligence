using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityAIAssistant.Core.Tools;

namespace UnityAIAssistant.Editor.Tools
{
    /// <summary>
    /// AI tools for material and shader operations.
    /// </summary>
    public static class MaterialTools
    {
        [AITool("create_material", "Create a new material")]
        public static AIToolResult CreateMaterial(
            [AIToolParameter("Path to save the material (e.g., 'Assets/Materials/NewMat.mat')")] string path,
            [AIToolParameter("Shader name (e.g., 'Standard', 'Universal Render Pipeline/Lit')", isOptional: true)] string shaderName = "Standard",
            [AIToolParameter("Main color in hex (e.g., '#FF0000')", isOptional: true)] string color = null)
        {
            try
            {
                // Ensure path ends with .mat
                if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".mat";
                }

                // Find shader
                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    // Try common alternatives
                    shader = Shader.Find("Universal Render Pipeline/Lit") ??
                             Shader.Find("Standard") ??
                             Shader.Find("Unlit/Color");
                }

                if (shader == null)
                {
                    return AIToolResult.Failed($"Shader not found: {shaderName}");
                }

                // Create directory if needed
                string directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                    AssetDatabase.Refresh();
                }

                // Create material
                var material = new Material(shader);

                if (!string.IsNullOrEmpty(color) && ColorUtility.TryParseHtmlString(color, out Color parsedColor))
                {
                    material.color = parsedColor;
                }

                AssetDatabase.CreateAsset(material, path);
                AssetDatabase.SaveAssets();

                return AIToolResult.Succeeded($"Created material at {path} using shader '{shader.name}'");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to create material: {ex.Message}");
            }
        }

        [AITool("get_material_properties", "Get all properties of a material")]
        public static AIToolResult GetMaterialProperties(
            [AIToolParameter("Path to the material asset")] string path)
        {
            try
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    return AIToolResult.Failed($"Material not found: {path}");
                }

                var shader = material.shader;
                var properties = new List<object>();

                int propertyCount = shader.GetPropertyCount();
                for (int i = 0; i < propertyCount; i++)
                {
                    string propName = shader.GetPropertyName(i);
                    var propType = shader.GetPropertyType(i);
                    string propDesc = shader.GetPropertyDescription(i);

                    object value = propType switch
                    {
                        ShaderPropertyType.Color => ColorToHex(material.GetColor(propName)),
                        ShaderPropertyType.Vector => VectorToArray(material.GetVector(propName)),
                        ShaderPropertyType.Float => material.GetFloat(propName),
                        ShaderPropertyType.Range => material.GetFloat(propName),
                        ShaderPropertyType.Texture => material.GetTexture(propName)?.name ?? "None",
                        ShaderPropertyType.Int => material.GetInt(propName),
                        _ => "Unknown"
                    };

                    properties.Add(new
                    {
                        name = propName,
                        type = propType.ToString(),
                        description = propDesc,
                        value
                    });
                }

                var info = new
                {
                    name = material.name,
                    path,
                    shader = shader.name,
                    renderQueue = material.renderQueue,
                    passCount = material.passCount,
                    properties
                };

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get material properties: {ex.Message}");
            }
        }

        [AITool("set_material_color", "Set a color property on a material", requiresConfirmation: true)]
        public static AIToolResult SetMaterialColor(
            [AIToolParameter("Path to the material asset")] string path,
            [AIToolParameter("Property name (e.g., '_Color', '_BaseColor')")] string propertyName,
            [AIToolParameter("Color in hex format (e.g., '#FF0000')")] string color)
        {
            try
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    return AIToolResult.Failed($"Material not found: {path}");
                }

                if (!material.HasProperty(propertyName))
                {
                    return AIToolResult.Failed($"Material does not have property: {propertyName}");
                }

                if (!ColorUtility.TryParseHtmlString(color, out Color parsedColor))
                {
                    return AIToolResult.Failed($"Invalid color format: {color}");
                }

                Undo.RecordObject(material, $"Set {propertyName}");
                material.SetColor(propertyName, parsedColor);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();

                return AIToolResult.Succeeded($"Set {propertyName} to {color} on {material.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set color: {ex.Message}");
            }
        }

        [AITool("set_material_float", "Set a float property on a material", requiresConfirmation: true)]
        public static AIToolResult SetMaterialFloat(
            [AIToolParameter("Path to the material asset")] string path,
            [AIToolParameter("Property name (e.g., '_Metallic', '_Smoothness')")] string propertyName,
            [AIToolParameter("Float value")] float value)
        {
            try
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    return AIToolResult.Failed($"Material not found: {path}");
                }

                if (!material.HasProperty(propertyName))
                {
                    return AIToolResult.Failed($"Material does not have property: {propertyName}");
                }

                Undo.RecordObject(material, $"Set {propertyName}");
                material.SetFloat(propertyName, value);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();

                return AIToolResult.Succeeded($"Set {propertyName} to {value} on {material.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set float: {ex.Message}");
            }
        }

        [AITool("set_material_texture", "Set a texture property on a material", requiresConfirmation: true)]
        public static AIToolResult SetMaterialTexture(
            [AIToolParameter("Path to the material asset")] string materialPath,
            [AIToolParameter("Property name (e.g., '_MainTex', '_BaseMap')")] string propertyName,
            [AIToolParameter("Path to the texture asset")] string texturePath)
        {
            try
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    return AIToolResult.Failed($"Material not found: {materialPath}");
                }

                if (!material.HasProperty(propertyName))
                {
                    return AIToolResult.Failed($"Material does not have property: {propertyName}");
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
                if (texture == null)
                {
                    return AIToolResult.Failed($"Texture not found: {texturePath}");
                }

                Undo.RecordObject(material, $"Set {propertyName}");
                material.SetTexture(propertyName, texture);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();

                return AIToolResult.Succeeded($"Set {propertyName} to {texture.name} on {material.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set texture: {ex.Message}");
            }
        }

        [AITool("assign_material", "Assign a material to a GameObject's renderer", requiresConfirmation: true)]
        public static AIToolResult AssignMaterial(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Path to the material asset")] string materialPath,
            [AIToolParameter("Material slot index (default 0)", isOptional: true)] int slot = 0)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                var renderer = go.GetComponent<Renderer>();
                if (renderer == null)
                {
                    return AIToolResult.Failed($"GameObject has no Renderer component");
                }

                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    return AIToolResult.Failed($"Material not found: {materialPath}");
                }

                var materials = renderer.sharedMaterials;
                if (slot < 0 || slot >= materials.Length)
                {
                    // Extend array if needed
                    var newMaterials = new Material[slot + 1];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        newMaterials[i] = materials[i];
                    }
                    materials = newMaterials;
                }

                Undo.RecordObject(renderer, "Assign Material");
                materials[slot] = material;
                renderer.sharedMaterials = materials;

                return AIToolResult.Succeeded($"Assigned {material.name} to {go.name} slot {slot}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to assign material: {ex.Message}");
            }
        }

        [AITool("list_shaders", "List available shaders")]
        public static AIToolResult ListShaders(
            [AIToolParameter("Filter by name (optional)", isOptional: true)] string filter = "")
        {
            try
            {
                var shaderGuids = AssetDatabase.FindAssets("t:Shader");
                var shaders = shaderGuids.Select(guid =>
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    return new
                    {
                        name = shader?.name ?? "Unknown",
                        path,
                        propertyCount = shader?.GetPropertyCount() ?? 0
                    };
                });

                if (!string.IsNullOrEmpty(filter))
                {
                    shaders = shaders.Where(s =>
                        s.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                var shaderList = shaders.Take(50).ToArray();

                // Also add built-in shaders
                var builtInShaders = new[]
                {
                    "Standard", "Standard (Specular setup)",
                    "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit",
                    "Unlit/Color", "Unlit/Texture",
                    "Particles/Standard Unlit", "Skybox/Procedural"
                };

                return AIToolResult.Succeeded($"Found {shaderList.Length} custom shaders", new
                {
                    customShaders = shaderList,
                    builtInShaders
                });
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to list shaders: {ex.Message}");
            }
        }

        [AITool("get_renderer_materials", "Get materials assigned to a GameObject's renderer")]
        public static AIToolResult GetRendererMaterials(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                var renderer = go.GetComponent<Renderer>();
                if (renderer == null)
                {
                    return AIToolResult.Failed($"GameObject has no Renderer component");
                }

                var materials = renderer.sharedMaterials.Select((mat, index) => new
                {
                    slot = index,
                    name = mat?.name ?? "None",
                    shader = mat?.shader?.name ?? "None",
                    path = mat != null ? AssetDatabase.GetAssetPath(mat) : null
                }).ToArray();

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(new
                {
                    gameObject = go.name,
                    rendererType = renderer.GetType().Name,
                    materials
                }, Formatting.Indented));
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get materials: {ex.Message}");
            }
        }

        #region Helpers

        private static string ColorToHex(Color color)
        {
            return $"#{ColorUtility.ToHtmlStringRGBA(color)}";
        }

        private static float[] VectorToArray(Vector4 v)
        {
            return new[] { v.x, v.y, v.z, v.w };
        }

        #endregion
    }
}
