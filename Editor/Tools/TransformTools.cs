using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityAIAssistant.Core.Tools;

namespace UnityAIAssistant.Editor.Tools
{
    /// <summary>
    /// AI tools for transform and hierarchy operations.
    /// </summary>
    public static class TransformTools
    {
        [AITool("set_position", "Set the position of a GameObject")]
        public static AIToolResult SetPosition(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("X position")] float x,
            [AIToolParameter("Y position")] float y,
            [AIToolParameter("Z position")] float z,
            [AIToolParameter("Use world space (true) or local space (false)", isOptional: true)] bool worldSpace = true)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                Undo.RecordObject(go.transform, "Set Position");
                if (worldSpace)
                {
                    go.transform.position = new Vector3(x, y, z);
                }
                else
                {
                    go.transform.localPosition = new Vector3(x, y, z);
                }

                return AIToolResult.Succeeded($"Set position of {go.name} to ({x}, {y}, {z})");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set position: {ex.Message}");
            }
        }

        [AITool("set_rotation", "Set the rotation of a GameObject")]
        public static AIToolResult SetRotation(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("X rotation (degrees)")] float x,
            [AIToolParameter("Y rotation (degrees)")] float y,
            [AIToolParameter("Z rotation (degrees)")] float z,
            [AIToolParameter("Use world space (true) or local space (false)", isOptional: true)] bool worldSpace = true)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                Undo.RecordObject(go.transform, "Set Rotation");
                if (worldSpace)
                {
                    go.transform.rotation = Quaternion.Euler(x, y, z);
                }
                else
                {
                    go.transform.localRotation = Quaternion.Euler(x, y, z);
                }

                return AIToolResult.Succeeded($"Set rotation of {go.name} to ({x}, {y}, {z})");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set rotation: {ex.Message}");
            }
        }

        [AITool("set_scale", "Set the local scale of a GameObject")]
        public static AIToolResult SetScale(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("X scale")] float x,
            [AIToolParameter("Y scale")] float y,
            [AIToolParameter("Z scale")] float z)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                Undo.RecordObject(go.transform, "Set Scale");
                go.transform.localScale = new Vector3(x, y, z);

                return AIToolResult.Succeeded($"Set scale of {go.name} to ({x}, {y}, {z})");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set scale: {ex.Message}");
            }
        }

        [AITool("set_transform", "Set position, rotation, and scale of a GameObject at once")]
        public static AIToolResult SetTransform(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Position X", isOptional: true)] float? posX = null,
            [AIToolParameter("Position Y", isOptional: true)] float? posY = null,
            [AIToolParameter("Position Z", isOptional: true)] float? posZ = null,
            [AIToolParameter("Rotation X (degrees)", isOptional: true)] float? rotX = null,
            [AIToolParameter("Rotation Y (degrees)", isOptional: true)] float? rotY = null,
            [AIToolParameter("Rotation Z (degrees)", isOptional: true)] float? rotZ = null,
            [AIToolParameter("Scale X", isOptional: true)] float? scaleX = null,
            [AIToolParameter("Scale Y", isOptional: true)] float? scaleY = null,
            [AIToolParameter("Scale Z", isOptional: true)] float? scaleZ = null)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                Undo.RecordObject(go.transform, "Set Transform");

                var pos = go.transform.localPosition;
                if (posX.HasValue) pos.x = posX.Value;
                if (posY.HasValue) pos.y = posY.Value;
                if (posZ.HasValue) pos.z = posZ.Value;
                go.transform.localPosition = pos;

                var rot = go.transform.localEulerAngles;
                if (rotX.HasValue) rot.x = rotX.Value;
                if (rotY.HasValue) rot.y = rotY.Value;
                if (rotZ.HasValue) rot.z = rotZ.Value;
                go.transform.localEulerAngles = rot;

                var scale = go.transform.localScale;
                if (scaleX.HasValue) scale.x = scaleX.Value;
                if (scaleY.HasValue) scale.y = scaleY.Value;
                if (scaleZ.HasValue) scale.z = scaleZ.Value;
                go.transform.localScale = scale;

                return AIToolResult.Succeeded($"Updated transform of {go.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set transform: {ex.Message}");
            }
        }

        [AITool("get_transform", "Get the transform of a GameObject")]
        public static AIToolResult GetTransform(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                var transform = go.transform;
                var info = new
                {
                    name = go.name,
                    worldPosition = new { x = transform.position.x, y = transform.position.y, z = transform.position.z },
                    localPosition = new { x = transform.localPosition.x, y = transform.localPosition.y, z = transform.localPosition.z },
                    worldRotation = new { x = transform.eulerAngles.x, y = transform.eulerAngles.y, z = transform.eulerAngles.z },
                    localRotation = new { x = transform.localEulerAngles.x, y = transform.localEulerAngles.y, z = transform.localEulerAngles.z },
                    localScale = new { x = transform.localScale.x, y = transform.localScale.y, z = transform.localScale.z },
                    lossyScale = new { x = transform.lossyScale.x, y = transform.lossyScale.y, z = transform.lossyScale.z }
                };

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get transform: {ex.Message}");
            }
        }

        [AITool("set_parent", "Set the parent of a GameObject")]
        public static AIToolResult SetParent(
            [AIToolParameter("Path or name of the child GameObject")] string childPath,
            [AIToolParameter("Path or name of the new parent (empty to unparent)")] string parentPath,
            [AIToolParameter("Keep world position unchanged", isOptional: true)] bool worldPositionStays = true)
        {
            try
            {
                var child = GameObject.Find(childPath);
                if (child == null)
                {
                    return AIToolResult.Failed($"Child GameObject not found: {childPath}");
                }

                Transform newParent = null;
                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parentGo = GameObject.Find(parentPath);
                    if (parentGo == null)
                    {
                        return AIToolResult.Failed($"Parent GameObject not found: {parentPath}");
                    }
                    newParent = parentGo.transform;
                }

                Undo.SetTransformParent(child.transform, newParent, $"Set Parent of {child.name}");
                child.transform.SetParent(newParent, worldPositionStays);

                string parentName = newParent?.name ?? "root";
                return AIToolResult.Succeeded($"Set parent of {child.name} to {parentName}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set parent: {ex.Message}");
            }
        }

        [AITool("set_sibling_index", "Set the sibling index (order in hierarchy) of a GameObject")]
        public static AIToolResult SetSiblingIndex(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("New sibling index (0 = first, -1 = last)")] int index)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                Undo.RecordObject(go.transform, "Set Sibling Index");
                if (index < 0)
                {
                    go.transform.SetAsLastSibling();
                }
                else if (index == 0)
                {
                    go.transform.SetAsFirstSibling();
                }
                else
                {
                    go.transform.SetSiblingIndex(index);
                }

                return AIToolResult.Succeeded($"Set sibling index of {go.name} to {go.transform.GetSiblingIndex()}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set sibling index: {ex.Message}");
            }
        }

        [AITool("duplicate_gameobject", "Duplicate a GameObject")]
        public static AIToolResult DuplicateGameObject(
            [AIToolParameter("Path or name of the GameObject to duplicate")] string gameObjectPath,
            [AIToolParameter("New name for the duplicate (optional)", isOptional: true)] string newName = null)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                var duplicate = UnityEngine.Object.Instantiate(go, go.transform.parent);
                duplicate.name = newName ?? (go.name + " (Copy)");
                Undo.RegisterCreatedObjectUndo(duplicate, $"Duplicate {go.name}");
                Selection.activeGameObject = duplicate;

                return AIToolResult.Succeeded($"Created duplicate: {duplicate.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to duplicate: {ex.Message}");
            }
        }

        [AITool("delete_gameobject", "Delete a GameObject from the scene", requiresConfirmation: true)]
        public static AIToolResult DeleteGameObject(
            [AIToolParameter("Path or name of the GameObject to delete")] string gameObjectPath)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                string name = go.name;
                Undo.DestroyObjectImmediate(go);

                return AIToolResult.Succeeded($"Deleted GameObject: {name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to delete: {ex.Message}");
            }
        }

        [AITool("set_active", "Set the active state of a GameObject")]
        public static AIToolResult SetActive(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Active state")] bool active)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    // Try to find inactive object
                    var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                    go = allObjects.FirstOrDefault(g =>
                        g.name == gameObjectPath || GetGameObjectPath(g) == gameObjectPath);
                }

                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                Undo.RecordObject(go, $"Set Active {active}");
                go.SetActive(active);

                return AIToolResult.Succeeded($"Set {go.name} active = {active}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set active: {ex.Message}");
            }
        }

        [AITool("set_layer", "Set the layer of a GameObject")]
        public static AIToolResult SetLayer(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Layer name or number")] string layer,
            [AIToolParameter("Apply to children as well", isOptional: true)] bool includeChildren = false)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                int layerIndex;
                if (int.TryParse(layer, out layerIndex))
                {
                    // Layer specified as number
                }
                else
                {
                    // Layer specified as name
                    layerIndex = LayerMask.NameToLayer(layer);
                    if (layerIndex < 0)
                    {
                        return AIToolResult.Failed($"Layer not found: {layer}");
                    }
                }

                Undo.RecordObject(go, "Set Layer");
                go.layer = layerIndex;

                if (includeChildren)
                {
                    foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                    {
                        Undo.RecordObject(child.gameObject, "Set Layer");
                        child.gameObject.layer = layerIndex;
                    }
                }

                return AIToolResult.Succeeded($"Set layer of {go.name} to {LayerMask.LayerToName(layerIndex)}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set layer: {ex.Message}");
            }
        }

        [AITool("set_tag", "Set the tag of a GameObject")]
        public static AIToolResult SetTag(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Tag name")] string tag)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                Undo.RecordObject(go, "Set Tag");
                go.tag = tag;

                return AIToolResult.Succeeded($"Set tag of {go.name} to {tag}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set tag: {ex.Message}");
            }
        }

        [AITool("look_at", "Make a GameObject look at a target position or object")]
        public static AIToolResult LookAt(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Target X (or target GameObject path)")] string targetX,
            [AIToolParameter("Target Y (optional if targeting object)", isOptional: true)] float? targetY = null,
            [AIToolParameter("Target Z (optional if targeting object)", isOptional: true)] float? targetZ = null)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                Vector3 targetPosition;

                // Check if targetX is a GameObject path or a number
                if (targetY.HasValue && targetZ.HasValue && float.TryParse(targetX, out float x))
                {
                    targetPosition = new Vector3(x, targetY.Value, targetZ.Value);
                }
                else
                {
                    var targetGo = GameObject.Find(targetX);
                    if (targetGo == null)
                    {
                        return AIToolResult.Failed($"Target GameObject not found: {targetX}");
                    }
                    targetPosition = targetGo.transform.position;
                }

                Undo.RecordObject(go.transform, "Look At");
                go.transform.LookAt(targetPosition);

                return AIToolResult.Succeeded($"{go.name} now looking at target");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to look at: {ex.Message}");
            }
        }

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
    }
}
