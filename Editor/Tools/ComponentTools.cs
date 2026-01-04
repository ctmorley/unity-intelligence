using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityAIAssistant.Core.Tools;

namespace UnityAIAssistant.Editor.Tools
{
    /// <summary>
    /// AI tools for component inspection and modification.
    /// </summary>
    public static class ComponentTools
    {
        [AITool("get_component", "Get detailed information about a component on a GameObject")]
        public static AIToolResult GetComponent(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Component type name (e.g., 'Transform', 'Rigidbody', 'MyScript')")] string componentType)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                var type = FindComponentType(componentType);
                if (type == null)
                {
                    return AIToolResult.Failed($"Component type not found: {componentType}");
                }

                var component = go.GetComponent(type);
                if (component == null)
                {
                    return AIToolResult.Failed($"Component not found on {go.name}: {componentType}");
                }

                var properties = GetSerializedProperties(component);

                var info = new
                {
                    gameObject = go.name,
                    componentType = type.Name,
                    fullTypeName = type.FullName,
                    enabled = component is Behaviour behaviour ? behaviour.enabled : true,
                    properties
                };

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get component: {ex.Message}");
            }
        }

        [AITool("get_all_components", "Get all components on a GameObject")]
        public static AIToolResult GetAllComponents(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Include children", isOptional: true)] bool includeChildren = false)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                Component[] components = includeChildren
                    ? go.GetComponentsInChildren<Component>(true)
                    : go.GetComponents<Component>();

                var componentInfos = components
                    .Where(c => c != null)
                    .Select(c => new
                    {
                        gameObject = c.gameObject.name,
                        type = c.GetType().Name,
                        fullType = c.GetType().FullName,
                        enabled = c is Behaviour b ? b.enabled : true
                    })
                    .ToArray();

                return AIToolResult.Succeeded($"Found {componentInfos.Length} components", new { components = componentInfos });
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get components: {ex.Message}");
            }
        }

        [AITool("add_component", "Add a component to a GameObject")]
        public static AIToolResult AddComponent(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Component type name (e.g., 'Rigidbody', 'BoxCollider', 'AudioSource')")] string componentType)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                var type = FindComponentType(componentType);
                if (type == null)
                {
                    return AIToolResult.Failed($"Component type not found: {componentType}");
                }

                Undo.RecordObject(go, $"Add {componentType}");
                var component = Undo.AddComponent(go, type);

                return AIToolResult.Succeeded($"Added {type.Name} to {go.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to add component: {ex.Message}");
            }
        }

        [AITool("remove_component", "Remove a component from a GameObject", requiresConfirmation: true)]
        public static AIToolResult RemoveComponent(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Component type name to remove")] string componentType)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                var type = FindComponentType(componentType);
                if (type == null)
                {
                    return AIToolResult.Failed($"Component type not found: {componentType}");
                }

                var component = go.GetComponent(type);
                if (component == null)
                {
                    return AIToolResult.Failed($"Component not found on {go.name}: {componentType}");
                }

                // Don't allow removing Transform
                if (component is Transform)
                {
                    return AIToolResult.Failed("Cannot remove Transform component");
                }

                Undo.DestroyObjectImmediate(component);
                return AIToolResult.Succeeded($"Removed {type.Name} from {go.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to remove component: {ex.Message}");
            }
        }

        [AITool("set_component_property", "Set a property value on a component", requiresConfirmation: true)]
        public static AIToolResult SetComponentProperty(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Component type name")] string componentType,
            [AIToolParameter("Property name")] string propertyName,
            [AIToolParameter("Value (JSON format for complex types)")] string value)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                var type = FindComponentType(componentType);
                if (type == null)
                {
                    return AIToolResult.Failed($"Component type not found: {componentType}");
                }

                var component = go.GetComponent(type);
                if (component == null)
                {
                    return AIToolResult.Failed($"Component not found on {go.name}: {componentType}");
                }

                // Use SerializedObject for proper undo support
                var serializedObject = new SerializedObject(component);
                var property = serializedObject.FindProperty(propertyName);

                if (property == null)
                {
                    // Try direct reflection as fallback
                    return SetPropertyViaReflection(component, propertyName, value);
                }

                Undo.RecordObject(component, $"Set {propertyName}");
                bool success = SetSerializedPropertyValue(property, value);

                if (success)
                {
                    serializedObject.ApplyModifiedProperties();
                    return AIToolResult.Succeeded($"Set {propertyName} = {value} on {componentType}");
                }
                else
                {
                    return AIToolResult.Failed($"Failed to set property: {propertyName}");
                }
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set property: {ex.Message}");
            }
        }

        [AITool("set_component_enabled", "Enable or disable a component")]
        public static AIToolResult SetComponentEnabled(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Component type name")] string componentType,
            [AIToolParameter("Enabled state")] bool enabled)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                var type = FindComponentType(componentType);
                if (type == null)
                {
                    return AIToolResult.Failed($"Component type not found: {componentType}");
                }

                var component = go.GetComponent(type);
                if (component == null)
                {
                    return AIToolResult.Failed($"Component not found on {go.name}: {componentType}");
                }

                if (component is Behaviour behaviour)
                {
                    Undo.RecordObject(behaviour, $"Set {componentType} enabled");
                    behaviour.enabled = enabled;
                    return AIToolResult.Succeeded($"Set {componentType} enabled = {enabled}");
                }
                else if (component is Renderer renderer)
                {
                    Undo.RecordObject(renderer, $"Set {componentType} enabled");
                    renderer.enabled = enabled;
                    return AIToolResult.Succeeded($"Set {componentType} enabled = {enabled}");
                }
                else if (component is Collider collider)
                {
                    Undo.RecordObject(collider, $"Set {componentType} enabled");
                    collider.enabled = enabled;
                    return AIToolResult.Succeeded($"Set {componentType} enabled = {enabled}");
                }
                else
                {
                    return AIToolResult.Failed($"Component {componentType} cannot be enabled/disabled");
                }
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set enabled: {ex.Message}");
            }
        }

        [AITool("copy_component", "Copy a component from one GameObject to another")]
        public static AIToolResult CopyComponent(
            [AIToolParameter("Path or name of the source GameObject")] string sourcePath,
            [AIToolParameter("Component type name to copy")] string componentType,
            [AIToolParameter("Path or name of the destination GameObject")] string destinationPath)
        {
            try
            {
                var sourceGo = GameObject.Find(sourcePath);
                if (sourceGo == null)
                {
                    return AIToolResult.Failed($"Source GameObject not found: {sourcePath}");
                }

                var destGo = GameObject.Find(destinationPath);
                if (destGo == null)
                {
                    return AIToolResult.Failed($"Destination GameObject not found: {destinationPath}");
                }

                var type = FindComponentType(componentType);
                if (type == null)
                {
                    return AIToolResult.Failed($"Component type not found: {componentType}");
                }

                var sourceComponent = sourceGo.GetComponent(type);
                if (sourceComponent == null)
                {
                    return AIToolResult.Failed($"Component not found on source: {componentType}");
                }

                // Use Unity's component copy functionality
                UnityEditorInternal.ComponentUtility.CopyComponent(sourceComponent);

                var existingComponent = destGo.GetComponent(type);
                if (existingComponent != null)
                {
                    Undo.RecordObject(existingComponent, "Paste Component Values");
                    UnityEditorInternal.ComponentUtility.PasteComponentValues(existingComponent);
                    return AIToolResult.Succeeded($"Pasted {type.Name} values to existing component on {destGo.name}");
                }
                else
                {
                    UnityEditorInternal.ComponentUtility.PasteComponentAsNew(destGo);
                    return AIToolResult.Succeeded($"Copied {type.Name} to {destGo.name}");
                }
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to copy component: {ex.Message}");
            }
        }

        [AITool("reset_component", "Reset a component to its default values", requiresConfirmation: true)]
        public static AIToolResult ResetComponent(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Component type name to reset")] string componentType)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                var type = FindComponentType(componentType);
                if (type == null)
                {
                    return AIToolResult.Failed($"Component type not found: {componentType}");
                }

                var component = go.GetComponent(type);
                if (component == null)
                {
                    return AIToolResult.Failed($"Component not found on {go.name}: {componentType}");
                }

                Undo.RecordObject(component, $"Reset {componentType}");

                // Use SerializedObject to reset
                var serializedObject = new SerializedObject(component);
                var property = serializedObject.GetIterator();

                while (property.NextVisible(true))
                {
                    if (property.propertyPath != "m_Script")
                    {
                        property.Reset();
                    }
                }

                serializedObject.ApplyModifiedProperties();

                return AIToolResult.Succeeded($"Reset {type.Name} on {go.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to reset component: {ex.Message}");
            }
        }

        [AITool("list_component_types", "List available component types by category")]
        public static AIToolResult ListComponentTypes(
            [AIToolParameter("Category filter (e.g., 'Physics', 'Rendering', 'Audio')", isOptional: true)] string category = "")
        {
            var categories = new Dictionary<string, string[]>
            {
                ["Physics"] = new[] { "Rigidbody", "Rigidbody2D", "BoxCollider", "SphereCollider", "CapsuleCollider",
                    "MeshCollider", "BoxCollider2D", "CircleCollider2D", "PolygonCollider2D", "CharacterController",
                    "FixedJoint", "HingeJoint", "SpringJoint", "ConfigurableJoint" },
                ["Rendering"] = new[] { "MeshRenderer", "SkinnedMeshRenderer", "SpriteRenderer", "LineRenderer",
                    "TrailRenderer", "ParticleSystemRenderer", "Light", "Camera", "ReflectionProbe", "LightProbeGroup" },
                ["Audio"] = new[] { "AudioSource", "AudioListener", "AudioReverbZone", "AudioReverbFilter",
                    "AudioChorusFilter", "AudioDistortionFilter", "AudioEchoFilter", "AudioHighPassFilter", "AudioLowPassFilter" },
                ["UI"] = new[] { "Canvas", "CanvasScaler", "GraphicRaycaster", "RectTransform", "Image", "Text",
                    "Button", "Toggle", "Slider", "Scrollbar", "Dropdown", "InputField", "ScrollRect" },
                ["Animation"] = new[] { "Animator", "Animation", "PlayableDirector" },
                ["Effects"] = new[] { "ParticleSystem", "TrailRenderer", "LineRenderer", "LensFlare", "Projector" },
                ["Navigation"] = new[] { "NavMeshAgent", "NavMeshObstacle", "OffMeshLink" },
                ["Miscellaneous"] = new[] { "Transform", "RectTransform", "CanvasRenderer", "EventSystem" }
            };

            if (!string.IsNullOrEmpty(category))
            {
                var key = categories.Keys.FirstOrDefault(k =>
                    k.Equals(category, StringComparison.OrdinalIgnoreCase));
                if (key != null)
                {
                    return AIToolResult.Succeeded(JsonConvert.SerializeObject(new { category = key, types = categories[key] }, Formatting.Indented));
                }
                return AIToolResult.Failed($"Category not found: {category}. Available: {string.Join(", ", categories.Keys)}");
            }

            return AIToolResult.Succeeded(JsonConvert.SerializeObject(categories, Formatting.Indented));
        }

        [AITool("find_components_of_type", "Find all GameObjects with a specific component type")]
        public static AIToolResult FindComponentsOfType(
            [AIToolParameter("Component type name")] string componentType,
            [AIToolParameter("Include inactive objects", isOptional: true)] bool includeInactive = false)
        {
            try
            {
                var type = FindComponentType(componentType);
                if (type == null)
                {
                    return AIToolResult.Failed($"Component type not found: {componentType}");
                }

                var components = UnityEngine.Object.FindObjectsByType(type,
                    includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

                var results = components.Cast<Component>().Select(c => new
                {
                    gameObject = c.gameObject.name,
                    path = GetGameObjectPath(c.gameObject),
                    enabled = c is Behaviour b ? b.enabled : true
                }).Take(100).ToArray();

                return AIToolResult.Succeeded($"Found {results.Length} objects with {componentType}", new { results });
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Search failed: {ex.Message}");
            }
        }

        #region Helpers

        private static Type FindComponentType(string typeName)
        {
            // Try exact match first
            var type = Type.GetType(typeName);
            if (type != null && typeof(Component).IsAssignableFrom(type))
                return type;

            // Try UnityEngine namespace
            type = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null && typeof(Component).IsAssignableFrom(type))
                return type;

            // Try UnityEngine.UI namespace
            type = Type.GetType($"UnityEngine.UI.{typeName}, UnityEngine.UI");
            if (type != null && typeof(Component).IsAssignableFrom(type))
                return type;

            // Search all loaded assemblies
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => typeof(Component).IsAssignableFrom(t) &&
                    (t.Name == typeName || t.FullName == typeName));
        }

        private static Dictionary<string, object> GetSerializedProperties(Component component)
        {
            var properties = new Dictionary<string, object>();
            var serializedObject = new SerializedObject(component);
            var property = serializedObject.GetIterator();

            while (property.NextVisible(true))
            {
                if (property.propertyPath == "m_Script") continue;

                try
                {
                    properties[property.propertyPath] = GetPropertyValue(property);
                }
                catch
                {
                    properties[property.propertyPath] = $"<{property.propertyType}>";
                }
            }

            return properties;
        }

        private static object GetPropertyValue(SerializedProperty property)
        {
            return property.propertyType switch
            {
                SerializedPropertyType.Integer => property.intValue,
                SerializedPropertyType.Boolean => property.boolValue,
                SerializedPropertyType.Float => property.floatValue,
                SerializedPropertyType.String => property.stringValue,
                SerializedPropertyType.Color => ColorToHex(property.colorValue),
                SerializedPropertyType.ObjectReference => property.objectReferenceValue?.name ?? "None",
                SerializedPropertyType.Enum => property.enumNames[property.enumValueIndex],
                SerializedPropertyType.Vector2 => new { x = property.vector2Value.x, y = property.vector2Value.y },
                SerializedPropertyType.Vector3 => new { x = property.vector3Value.x, y = property.vector3Value.y, z = property.vector3Value.z },
                SerializedPropertyType.Vector4 => new { x = property.vector4Value.x, y = property.vector4Value.y, z = property.vector4Value.z, w = property.vector4Value.w },
                SerializedPropertyType.Rect => new { x = property.rectValue.x, y = property.rectValue.y, width = property.rectValue.width, height = property.rectValue.height },
                SerializedPropertyType.Bounds => new { center = property.boundsValue.center.ToString(), size = property.boundsValue.size.ToString() },
                SerializedPropertyType.Quaternion => new { x = property.quaternionValue.x, y = property.quaternionValue.y, z = property.quaternionValue.z, w = property.quaternionValue.w },
                SerializedPropertyType.LayerMask => property.intValue,
                _ => $"<{property.propertyType}>"
            };
        }

        private static bool SetSerializedPropertyValue(SerializedProperty property, string value)
        {
            try
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        property.intValue = int.Parse(value);
                        return true;
                    case SerializedPropertyType.Boolean:
                        property.boolValue = bool.Parse(value);
                        return true;
                    case SerializedPropertyType.Float:
                        property.floatValue = float.Parse(value);
                        return true;
                    case SerializedPropertyType.String:
                        property.stringValue = value;
                        return true;
                    case SerializedPropertyType.Color:
                        if (ColorUtility.TryParseHtmlString(value, out Color color))
                        {
                            property.colorValue = color;
                            return true;
                        }
                        return false;
                    case SerializedPropertyType.Enum:
                        var index = Array.IndexOf(property.enumNames, value);
                        if (index >= 0)
                        {
                            property.enumValueIndex = index;
                            return true;
                        }
                        if (int.TryParse(value, out int enumIndex))
                        {
                            property.enumValueIndex = enumIndex;
                            return true;
                        }
                        return false;
                    case SerializedPropertyType.Vector2:
                        var v2 = JsonConvert.DeserializeObject<Vector2Data>(value);
                        property.vector2Value = new Vector2(v2.x, v2.y);
                        return true;
                    case SerializedPropertyType.Vector3:
                        var v3 = JsonConvert.DeserializeObject<Vector3Data>(value);
                        property.vector3Value = new Vector3(v3.x, v3.y, v3.z);
                        return true;
                    case SerializedPropertyType.LayerMask:
                        property.intValue = int.Parse(value);
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private static AIToolResult SetPropertyViaReflection(Component component, string propertyName, string value)
        {
            var type = component.GetType();
            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            var field = type.GetField(propertyName, BindingFlags.Public | BindingFlags.Instance);

            if (prop != null && prop.CanWrite)
            {
                Undo.RecordObject(component, $"Set {propertyName}");
                var convertedValue = ConvertValue(value, prop.PropertyType);
                prop.SetValue(component, convertedValue);
                return AIToolResult.Succeeded($"Set {propertyName} via reflection");
            }
            else if (field != null)
            {
                Undo.RecordObject(component, $"Set {propertyName}");
                var convertedValue = ConvertValue(value, field.FieldType);
                field.SetValue(component, convertedValue);
                return AIToolResult.Succeeded($"Set {propertyName} via reflection");
            }

            return AIToolResult.Failed($"Property not found or not writable: {propertyName}");
        }

        private static object ConvertValue(string value, Type targetType)
        {
            if (targetType == typeof(int)) return int.Parse(value);
            if (targetType == typeof(float)) return float.Parse(value);
            if (targetType == typeof(bool)) return bool.Parse(value);
            if (targetType == typeof(string)) return value;
            if (targetType == typeof(Vector2))
            {
                var data = JsonConvert.DeserializeObject<Vector2Data>(value);
                return new Vector2(data.x, data.y);
            }
            if (targetType == typeof(Vector3))
            {
                var data = JsonConvert.DeserializeObject<Vector3Data>(value);
                return new Vector3(data.x, data.y, data.z);
            }
            if (targetType == typeof(Color))
            {
                ColorUtility.TryParseHtmlString(value, out Color color);
                return color;
            }
            return JsonConvert.DeserializeObject(value, targetType);
        }

        private static string ColorToHex(Color color)
        {
            return $"#{ColorUtility.ToHtmlStringRGBA(color)}";
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

        private struct Vector2Data { public float x, y; }
        private struct Vector3Data { public float x, y, z; }

        #endregion
    }
}
