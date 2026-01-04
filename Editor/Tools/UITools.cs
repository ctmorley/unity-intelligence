using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityAIAssistant.Core.Tools;

namespace UnityAIAssistant.Editor.Tools
{
    /// <summary>
    /// AI tools for Unity UI operations.
    /// </summary>
    public static class UITools
    {
        #region Canvas

        [AITool("create_canvas", "Create a new Canvas in the scene")]
        public static AIToolResult CreateCanvas(
            [AIToolParameter("Name for the canvas", isOptional: true)] string name = "Canvas",
            [AIToolParameter("Render mode: 'overlay', 'camera', 'worldSpace'", isOptional: true)] string renderMode = "overlay",
            [AIToolParameter("Camera path for 'camera' mode", isOptional: true)] string cameraPath = null)
        {
            try
            {
                // Create Canvas GameObject
                var canvasGo = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");

                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = renderMode.ToLower() switch
                {
                    "camera" => RenderMode.ScreenSpaceCamera,
                    "worldspace" => RenderMode.WorldSpace,
                    _ => RenderMode.ScreenSpaceOverlay
                };

                if (canvas.renderMode == RenderMode.ScreenSpaceCamera && !string.IsNullOrEmpty(cameraPath))
                {
                    var cameraGo = GameObject.Find(cameraPath);
                    if (cameraGo != null)
                    {
                        canvas.worldCamera = cameraGo.GetComponent<Camera>();
                    }
                }

                canvasGo.AddComponent<CanvasScaler>();
                canvasGo.AddComponent<GraphicRaycaster>();

                // Ensure EventSystem exists
                if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
                {
                    var eventSystemGo = new GameObject("EventSystem");
                    Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");
                    eventSystemGo.AddComponent<EventSystem>();
                    eventSystemGo.AddComponent<StandaloneInputModule>();
                }

                Selection.activeGameObject = canvasGo;
                return AIToolResult.Succeeded($"Created Canvas '{name}' with render mode: {renderMode}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to create canvas: {ex.Message}");
            }
        }

        [AITool("configure_canvas_scaler", "Configure a CanvasScaler component")]
        public static AIToolResult ConfigureCanvasScaler(
            [AIToolParameter("Path or name of the Canvas GameObject")] string canvasPath,
            [AIToolParameter("Scale mode: 'constantPixelSize', 'scaleWithScreenSize', 'constantPhysicalSize'", isOptional: true)] string scaleMode = null,
            [AIToolParameter("Reference resolution width (for scaleWithScreenSize)", isOptional: true)] float? referenceWidth = null,
            [AIToolParameter("Reference resolution height (for scaleWithScreenSize)", isOptional: true)] float? referenceHeight = null,
            [AIToolParameter("Match width or height (0 = width, 1 = height)", isOptional: true)] float? matchWidthOrHeight = null,
            [AIToolParameter("Scale factor (for constantPixelSize)", isOptional: true)] float? scaleFactor = null)
        {
            try
            {
                var go = GameObject.Find(canvasPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {canvasPath}");
                }

                var scaler = go.GetComponent<CanvasScaler>();
                if (scaler == null)
                {
                    return AIToolResult.Failed("GameObject does not have a CanvasScaler");
                }

                Undo.RecordObject(scaler, "Configure CanvasScaler");

                if (!string.IsNullOrEmpty(scaleMode))
                {
                    scaler.uiScaleMode = scaleMode.ToLower() switch
                    {
                        "scalewithscreensize" => CanvasScaler.ScaleMode.ScaleWithScreenSize,
                        "constantphysicalsize" => CanvasScaler.ScaleMode.ConstantPhysicalSize,
                        _ => CanvasScaler.ScaleMode.ConstantPixelSize
                    };
                }

                if (referenceWidth.HasValue || referenceHeight.HasValue)
                {
                    var refRes = scaler.referenceResolution;
                    if (referenceWidth.HasValue) refRes.x = referenceWidth.Value;
                    if (referenceHeight.HasValue) refRes.y = referenceHeight.Value;
                    scaler.referenceResolution = refRes;
                }

                if (matchWidthOrHeight.HasValue)
                {
                    scaler.matchWidthOrHeight = Mathf.Clamp01(matchWidthOrHeight.Value);
                }

                if (scaleFactor.HasValue)
                {
                    scaler.scaleFactor = scaleFactor.Value;
                }

                return AIToolResult.Succeeded($"Configured CanvasScaler on {go.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to configure CanvasScaler: {ex.Message}");
            }
        }

        #endregion

        #region UI Elements

        [AITool("create_ui_element", "Create a UI element")]
        public static AIToolResult CreateUIElement(
            [AIToolParameter("Element type: 'text', 'image', 'button', 'toggle', 'slider', 'inputfield', 'dropdown', 'scrollview', 'panel'")] string elementType,
            [AIToolParameter("Parent Canvas or UI element path")] string parentPath,
            [AIToolParameter("Name for the element", isOptional: true)] string name = null)
        {
            try
            {
                var parent = GameObject.Find(parentPath);
                if (parent == null)
                {
                    return AIToolResult.Failed($"Parent not found: {parentPath}");
                }

                // Ensure parent has RectTransform (is a UI element)
                if (parent.GetComponent<RectTransform>() == null && parent.GetComponent<Canvas>() == null)
                {
                    return AIToolResult.Failed("Parent must be a Canvas or UI element");
                }

                GameObject element = elementType.ToLower() switch
                {
                    "text" => CreateText(parent.transform, name ?? "Text"),
                    "image" => CreateImage(parent.transform, name ?? "Image"),
                    "button" => CreateButton(parent.transform, name ?? "Button"),
                    "toggle" => CreateToggle(parent.transform, name ?? "Toggle"),
                    "slider" => CreateSlider(parent.transform, name ?? "Slider"),
                    "inputfield" => CreateInputField(parent.transform, name ?? "InputField"),
                    "dropdown" => CreateDropdown(parent.transform, name ?? "Dropdown"),
                    "scrollview" => CreateScrollView(parent.transform, name ?? "ScrollView"),
                    "panel" => CreatePanel(parent.transform, name ?? "Panel"),
                    _ => null
                };

                if (element == null)
                {
                    return AIToolResult.Failed($"Unknown element type: {elementType}");
                }

                Undo.RegisterCreatedObjectUndo(element, $"Create {elementType}");
                Selection.activeGameObject = element;

                return AIToolResult.Succeeded($"Created {elementType} '{element.name}'");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to create UI element: {ex.Message}");
            }
        }

        [AITool("set_rect_transform", "Set RectTransform properties of a UI element")]
        public static AIToolResult SetRectTransform(
            [AIToolParameter("Path or name of the UI element")] string elementPath,
            [AIToolParameter("Anchor preset: 'topLeft', 'topCenter', 'topRight', 'middleLeft', 'middleCenter', 'middleRight', 'bottomLeft', 'bottomCenter', 'bottomRight', 'stretchTop', 'stretchMiddle', 'stretchBottom', 'stretchLeft', 'stretchCenter', 'stretchRight', 'stretchAll'", isOptional: true)] string anchorPreset = null,
            [AIToolParameter("Position X", isOptional: true)] float? posX = null,
            [AIToolParameter("Position Y", isOptional: true)] float? posY = null,
            [AIToolParameter("Width", isOptional: true)] float? width = null,
            [AIToolParameter("Height", isOptional: true)] float? height = null,
            [AIToolParameter("Pivot X (0-1)", isOptional: true)] float? pivotX = null,
            [AIToolParameter("Pivot Y (0-1)", isOptional: true)] float? pivotY = null)
        {
            try
            {
                var go = GameObject.Find(elementPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {elementPath}");
                }

                var rectTransform = go.GetComponent<RectTransform>();
                if (rectTransform == null)
                {
                    return AIToolResult.Failed("GameObject does not have a RectTransform");
                }

                Undo.RecordObject(rectTransform, "Set RectTransform");

                if (!string.IsNullOrEmpty(anchorPreset))
                {
                    ApplyAnchorPreset(rectTransform, anchorPreset);
                }

                if (posX.HasValue || posY.HasValue)
                {
                    var pos = rectTransform.anchoredPosition;
                    if (posX.HasValue) pos.x = posX.Value;
                    if (posY.HasValue) pos.y = posY.Value;
                    rectTransform.anchoredPosition = pos;
                }

                if (width.HasValue || height.HasValue)
                {
                    var size = rectTransform.sizeDelta;
                    if (width.HasValue) size.x = width.Value;
                    if (height.HasValue) size.y = height.Value;
                    rectTransform.sizeDelta = size;
                }

                if (pivotX.HasValue || pivotY.HasValue)
                {
                    var pivot = rectTransform.pivot;
                    if (pivotX.HasValue) pivot.x = pivotX.Value;
                    if (pivotY.HasValue) pivot.y = pivotY.Value;
                    rectTransform.pivot = pivot;
                }

                return AIToolResult.Succeeded($"Updated RectTransform on {go.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set RectTransform: {ex.Message}");
            }
        }

        [AITool("set_text_properties", "Set Text or TextMeshPro component properties")]
        public static AIToolResult SetTextProperties(
            [AIToolParameter("Path or name of the UI element")] string elementPath,
            [AIToolParameter("Text content", isOptional: true)] string text = null,
            [AIToolParameter("Font size", isOptional: true)] int? fontSize = null,
            [AIToolParameter("Color in hex (e.g., '#FFFFFF')", isOptional: true)] string color = null,
            [AIToolParameter("Alignment: 'left', 'center', 'right'", isOptional: true)] string alignment = null,
            [AIToolParameter("Font style: 'normal', 'bold', 'italic', 'boldItalic'", isOptional: true)] string fontStyle = null)
        {
            try
            {
                var go = GameObject.Find(elementPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {elementPath}");
                }

                var textComponent = go.GetComponent<Text>();
                if (textComponent != null)
                {
                    Undo.RecordObject(textComponent, "Set Text Properties");

                    if (text != null) textComponent.text = text;
                    if (fontSize.HasValue) textComponent.fontSize = fontSize.Value;
                    if (!string.IsNullOrEmpty(color) && ColorUtility.TryParseHtmlString(color, out Color parsedColor))
                    {
                        textComponent.color = parsedColor;
                    }
                    if (!string.IsNullOrEmpty(alignment))
                    {
                        textComponent.alignment = alignment.ToLower() switch
                        {
                            "left" => TextAnchor.MiddleLeft,
                            "right" => TextAnchor.MiddleRight,
                            _ => TextAnchor.MiddleCenter
                        };
                    }
                    if (!string.IsNullOrEmpty(fontStyle))
                    {
                        textComponent.fontStyle = fontStyle.ToLower() switch
                        {
                            "bold" => FontStyle.Bold,
                            "italic" => FontStyle.Italic,
                            "bolditalic" => FontStyle.BoldAndItalic,
                            _ => FontStyle.Normal
                        };
                    }

                    return AIToolResult.Succeeded($"Updated Text on {go.name}");
                }

                return AIToolResult.Failed("GameObject does not have a Text component");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set text properties: {ex.Message}");
            }
        }

        [AITool("set_image_properties", "Set Image component properties")]
        public static AIToolResult SetImageProperties(
            [AIToolParameter("Path or name of the UI element")] string elementPath,
            [AIToolParameter("Sprite asset path", isOptional: true)] string spritePath = null,
            [AIToolParameter("Color in hex (e.g., '#FFFFFF')", isOptional: true)] string color = null,
            [AIToolParameter("Image type: 'simple', 'sliced', 'tiled', 'filled'", isOptional: true)] string imageType = null,
            [AIToolParameter("Preserve aspect ratio", isOptional: true)] bool? preserveAspect = null,
            [AIToolParameter("Raycast target", isOptional: true)] bool? raycastTarget = null)
        {
            try
            {
                var go = GameObject.Find(elementPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {elementPath}");
                }

                var image = go.GetComponent<Image>();
                if (image == null)
                {
                    return AIToolResult.Failed("GameObject does not have an Image component");
                }

                Undo.RecordObject(image, "Set Image Properties");

                if (!string.IsNullOrEmpty(spritePath))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                    if (sprite != null)
                    {
                        image.sprite = sprite;
                    }
                }

                if (!string.IsNullOrEmpty(color) && ColorUtility.TryParseHtmlString(color, out Color parsedColor))
                {
                    image.color = parsedColor;
                }

                if (!string.IsNullOrEmpty(imageType))
                {
                    image.type = imageType.ToLower() switch
                    {
                        "sliced" => Image.Type.Sliced,
                        "tiled" => Image.Type.Tiled,
                        "filled" => Image.Type.Filled,
                        _ => Image.Type.Simple
                    };
                }

                if (preserveAspect.HasValue) image.preserveAspect = preserveAspect.Value;
                if (raycastTarget.HasValue) image.raycastTarget = raycastTarget.Value;

                return AIToolResult.Succeeded($"Updated Image on {go.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set image properties: {ex.Message}");
            }
        }

        [AITool("set_button_properties", "Set Button component properties")]
        public static AIToolResult SetButtonProperties(
            [AIToolParameter("Path or name of the Button")] string elementPath,
            [AIToolParameter("Interactable", isOptional: true)] bool? interactable = null,
            [AIToolParameter("Normal color in hex", isOptional: true)] string normalColor = null,
            [AIToolParameter("Highlighted color in hex", isOptional: true)] string highlightedColor = null,
            [AIToolParameter("Pressed color in hex", isOptional: true)] string pressedColor = null,
            [AIToolParameter("Disabled color in hex", isOptional: true)] string disabledColor = null,
            [AIToolParameter("Button text content", isOptional: true)] string buttonText = null)
        {
            try
            {
                var go = GameObject.Find(elementPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {elementPath}");
                }

                var button = go.GetComponent<Button>();
                if (button == null)
                {
                    return AIToolResult.Failed("GameObject does not have a Button component");
                }

                Undo.RecordObject(button, "Set Button Properties");

                if (interactable.HasValue) button.interactable = interactable.Value;

                var colors = button.colors;
                if (!string.IsNullOrEmpty(normalColor) && ColorUtility.TryParseHtmlString(normalColor, out Color nc))
                    colors.normalColor = nc;
                if (!string.IsNullOrEmpty(highlightedColor) && ColorUtility.TryParseHtmlString(highlightedColor, out Color hc))
                    colors.highlightedColor = hc;
                if (!string.IsNullOrEmpty(pressedColor) && ColorUtility.TryParseHtmlString(pressedColor, out Color pc))
                    colors.pressedColor = pc;
                if (!string.IsNullOrEmpty(disabledColor) && ColorUtility.TryParseHtmlString(disabledColor, out Color dc))
                    colors.disabledColor = dc;
                button.colors = colors;

                // Set button text if provided
                if (!string.IsNullOrEmpty(buttonText))
                {
                    var textComponent = go.GetComponentInChildren<Text>();
                    if (textComponent != null)
                    {
                        Undo.RecordObject(textComponent, "Set Button Text");
                        textComponent.text = buttonText;
                    }
                }

                return AIToolResult.Succeeded($"Updated Button on {go.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set button properties: {ex.Message}");
            }
        }

        [AITool("get_ui_hierarchy", "Get the UI hierarchy of a Canvas")]
        public static AIToolResult GetUIHierarchy(
            [AIToolParameter("Path or name of the Canvas")] string canvasPath,
            [AIToolParameter("Maximum depth to traverse", isOptional: true)] int maxDepth = 10)
        {
            try
            {
                var go = GameObject.Find(canvasPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {canvasPath}");
                }

                var canvas = go.GetComponent<Canvas>();
                if (canvas == null)
                {
                    return AIToolResult.Failed("GameObject does not have a Canvas component");
                }

                var hierarchy = BuildUIHierarchy(go, 0, maxDepth);

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(new
                {
                    canvasName = go.name,
                    renderMode = canvas.renderMode.ToString(),
                    hierarchy
                }, Formatting.Indented));
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get UI hierarchy: {ex.Message}");
            }
        }

        [AITool("find_ui_elements", "Find UI elements by type")]
        public static AIToolResult FindUIElements(
            [AIToolParameter("Element type: 'text', 'image', 'button', 'toggle', 'slider', 'inputfield', 'dropdown', 'scrollrect'")] string elementType)
        {
            try
            {
                Type type = elementType.ToLower() switch
                {
                    "text" => typeof(Text),
                    "image" => typeof(Image),
                    "button" => typeof(Button),
                    "toggle" => typeof(Toggle),
                    "slider" => typeof(Slider),
                    "inputfield" => typeof(InputField),
                    "dropdown" => typeof(Dropdown),
                    "scrollrect" => typeof(ScrollRect),
                    _ => null
                };

                if (type == null)
                {
                    return AIToolResult.Failed($"Unknown element type: {elementType}");
                }

                var components = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None);
                var elements = components.Cast<Component>().Select(c => new
                {
                    name = c.gameObject.name,
                    path = GetGameObjectPath(c.gameObject),
                    active = c.gameObject.activeInHierarchy
                }).ToArray();

                return AIToolResult.Succeeded($"Found {elements.Length} {elementType} elements", new { elements });
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Search failed: {ex.Message}");
            }
        }

        #endregion

        #region Layout

        [AITool("add_layout_group", "Add a layout group to a UI element")]
        public static AIToolResult AddLayoutGroup(
            [AIToolParameter("Path or name of the UI element")] string elementPath,
            [AIToolParameter("Layout type: 'horizontal', 'vertical', 'grid'")] string layoutType,
            [AIToolParameter("Spacing", isOptional: true)] float spacing = 0,
            [AIToolParameter("Padding (all sides)", isOptional: true)] int padding = 0,
            [AIToolParameter("Child alignment: 'upperLeft', 'upperCenter', 'upperRight', 'middleLeft', 'middleCenter', 'middleRight', 'lowerLeft', 'lowerCenter', 'lowerRight'", isOptional: true)] string childAlignment = "upperLeft")
        {
            try
            {
                var go = GameObject.Find(elementPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {elementPath}");
                }

                HorizontalOrVerticalLayoutGroup layoutGroup = null;
                GridLayoutGroup gridLayout = null;

                switch (layoutType.ToLower())
                {
                    case "horizontal":
                        layoutGroup = Undo.AddComponent<HorizontalLayoutGroup>(go);
                        break;
                    case "vertical":
                        layoutGroup = Undo.AddComponent<VerticalLayoutGroup>(go);
                        break;
                    case "grid":
                        gridLayout = Undo.AddComponent<GridLayoutGroup>(go);
                        gridLayout.spacing = new Vector2(spacing, spacing);
                        gridLayout.padding = new RectOffset(padding, padding, padding, padding);
                        gridLayout.childAlignment = ParseTextAnchor(childAlignment);
                        return AIToolResult.Succeeded($"Added GridLayoutGroup to {go.name}");
                    default:
                        return AIToolResult.Failed($"Unknown layout type: {layoutType}");
                }

                if (layoutGroup != null)
                {
                    layoutGroup.spacing = spacing;
                    layoutGroup.padding = new RectOffset(padding, padding, padding, padding);
                    layoutGroup.childAlignment = ParseTextAnchor(childAlignment);
                    return AIToolResult.Succeeded($"Added {layoutType}LayoutGroup to {go.name}");
                }

                return AIToolResult.Failed("Failed to add layout group");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to add layout group: {ex.Message}");
            }
        }

        [AITool("add_content_size_fitter", "Add a ContentSizeFitter to a UI element")]
        public static AIToolResult AddContentSizeFitter(
            [AIToolParameter("Path or name of the UI element")] string elementPath,
            [AIToolParameter("Horizontal fit: 'unconstrained', 'minSize', 'preferredSize'", isOptional: true)] string horizontalFit = "unconstrained",
            [AIToolParameter("Vertical fit: 'unconstrained', 'minSize', 'preferredSize'", isOptional: true)] string verticalFit = "unconstrained")
        {
            try
            {
                var go = GameObject.Find(elementPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {elementPath}");
                }

                var fitter = go.GetComponent<ContentSizeFitter>();
                if (fitter == null)
                {
                    fitter = Undo.AddComponent<ContentSizeFitter>(go);
                }
                else
                {
                    Undo.RecordObject(fitter, "Configure ContentSizeFitter");
                }

                fitter.horizontalFit = ParseFitMode(horizontalFit);
                fitter.verticalFit = ParseFitMode(verticalFit);

                return AIToolResult.Succeeded($"Added ContentSizeFitter to {go.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to add ContentSizeFitter: {ex.Message}");
            }
        }

        #endregion

        #region Helpers

        private static GameObject CreateText(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = "New Text";
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 30);
            return go;
        }

        private static GameObject CreateImage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 100);
            return go;
        }

        private static GameObject CreateButton(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 30);

            var textGo = CreateText(go.transform, "Text");
            var text = textGo.GetComponent<Text>();
            text.text = "Button";
            text.alignment = TextAnchor.MiddleCenter;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            return go;
        }

        private static GameObject CreateToggle(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 20);

            var background = CreateImage(go.transform, "Background");
            background.GetComponent<RectTransform>().sizeDelta = new Vector2(20, 20);

            var checkmark = CreateImage(background.transform, "Checkmark");
            checkmark.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);

            var label = CreateText(go.transform, "Label");
            label.GetComponent<Text>().text = "Toggle";

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = background.GetComponent<Image>();
            toggle.graphic = checkmark.GetComponent<Image>();

            return go;
        }

        private static GameObject CreateSlider(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 20);

            var background = CreateImage(go.transform, "Background");
            var bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);
            bgRect.sizeDelta = Vector2.zero;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-15, 0);

            var fill = CreateImage(fillArea.transform, "Fill");
            fill.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);
            fill.GetComponent<RectTransform>().sizeDelta = new Vector2(10, 0);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);

            var handle = CreateImage(handleArea.transform, "Handle");
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(20, 0);

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();

            return go;
        }

        private static GameObject CreateInputField(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 30);

            var placeholder = CreateText(go.transform, "Placeholder");
            placeholder.GetComponent<Text>().text = "Enter text...";
            placeholder.GetComponent<Text>().fontStyle = FontStyle.Italic;
            placeholder.GetComponent<Text>().color = new Color(0.5f, 0.5f, 0.5f);
            var phRect = placeholder.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(10, 6);
            phRect.offsetMax = new Vector2(-10, -7);

            var textComponent = CreateText(go.transform, "Text");
            var textRect = textComponent.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 6);
            textRect.offsetMax = new Vector2(-10, -7);

            var inputField = go.GetComponent<InputField>();
            inputField.textComponent = textComponent.GetComponent<Text>();
            inputField.placeholder = placeholder.GetComponent<Text>();

            return go;
        }

        private static GameObject CreateDropdown(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Dropdown));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 30);

            var label = CreateText(go.transform, "Label");
            label.GetComponent<Text>().text = "Option A";
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10, 6);
            labelRect.offsetMax = new Vector2(-25, -7);

            var dropdown = go.GetComponent<Dropdown>();
            dropdown.captionText = label.GetComponent<Text>();
            dropdown.options.Add(new Dropdown.OptionData("Option A"));
            dropdown.options.Add(new Dropdown.OptionData("Option B"));
            dropdown.options.Add(new Dropdown.OptionData("Option C"));

            return go;
        }

        private static GameObject CreateScrollView(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 200);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(go.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.pivot = new Vector2(0, 1);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.sizeDelta = new Vector2(0, 300);
            contentRect.pivot = new Vector2(0, 1);

            var scrollRect = go.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;

            return go;
        }

        private static GameObject CreatePanel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            go.GetComponent<Image>().color = new Color(1, 1, 1, 0.4f);
            return go;
        }

        private static void ApplyAnchorPreset(RectTransform rectTransform, string preset)
        {
            switch (preset.ToLower())
            {
                case "topleft":
                    rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0, 1);
                    break;
                case "topcenter":
                    rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 1);
                    break;
                case "topright":
                    rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(1, 1);
                    break;
                case "middleleft":
                    rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0, 0.5f);
                    break;
                case "middlecenter":
                    rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    break;
                case "middleright":
                    rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(1, 0.5f);
                    break;
                case "bottomleft":
                    rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0, 0);
                    break;
                case "bottomcenter":
                    rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0);
                    break;
                case "bottomright":
                    rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(1, 0);
                    break;
                case "stretchall":
                    rectTransform.anchorMin = Vector2.zero;
                    rectTransform.anchorMax = Vector2.one;
                    break;
            }
        }

        private static TextAnchor ParseTextAnchor(string alignment)
        {
            return alignment.ToLower() switch
            {
                "upperleft" => TextAnchor.UpperLeft,
                "uppercenter" => TextAnchor.UpperCenter,
                "upperright" => TextAnchor.UpperRight,
                "middleleft" => TextAnchor.MiddleLeft,
                "middlecenter" => TextAnchor.MiddleCenter,
                "middleright" => TextAnchor.MiddleRight,
                "lowerleft" => TextAnchor.LowerLeft,
                "lowercenter" => TextAnchor.LowerCenter,
                "lowerright" => TextAnchor.LowerRight,
                _ => TextAnchor.UpperLeft
            };
        }

        private static ContentSizeFitter.FitMode ParseFitMode(string mode)
        {
            return mode.ToLower() switch
            {
                "minsize" => ContentSizeFitter.FitMode.MinSize,
                "preferredsize" => ContentSizeFitter.FitMode.PreferredSize,
                _ => ContentSizeFitter.FitMode.Unconstrained
            };
        }

        private static object BuildUIHierarchy(GameObject go, int depth, int maxDepth)
        {
            var components = new List<string>();
            if (go.GetComponent<Canvas>() != null) components.Add("Canvas");
            if (go.GetComponent<Text>() != null) components.Add("Text");
            if (go.GetComponent<Image>() != null) components.Add("Image");
            if (go.GetComponent<Button>() != null) components.Add("Button");
            if (go.GetComponent<Toggle>() != null) components.Add("Toggle");
            if (go.GetComponent<Slider>() != null) components.Add("Slider");
            if (go.GetComponent<InputField>() != null) components.Add("InputField");
            if (go.GetComponent<Dropdown>() != null) components.Add("Dropdown");
            if (go.GetComponent<ScrollRect>() != null) components.Add("ScrollRect");

            var children = new List<object>();
            if (depth < maxDepth)
            {
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    children.Add(BuildUIHierarchy(go.transform.GetChild(i).gameObject, depth + 1, maxDepth));
                }
            }

            return new
            {
                name = go.name,
                active = go.activeSelf,
                uiComponents = components.Count > 0 ? components : null,
                children = children.Count > 0 ? children : null
            };
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

        #endregion
    }
}
