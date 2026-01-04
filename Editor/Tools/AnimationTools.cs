using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityAIAssistant.Core.Tools;

namespace UnityAIAssistant.Editor.Tools
{
    /// <summary>
    /// AI tools for animation operations.
    /// </summary>
    public static class AnimationTools
    {
        #region Animator

        [AITool("add_animator", "Add an Animator component to a GameObject")]
        public static AIToolResult AddAnimator(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Path to animator controller asset (optional)", isOptional: true)] string controllerPath = null)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                if (go.GetComponent<Animator>() != null)
                {
                    return AIToolResult.Failed("GameObject already has an Animator");
                }

                var animator = Undo.AddComponent<Animator>(go);

                if (!string.IsNullOrEmpty(controllerPath))
                {
                    var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
                    if (controller != null)
                    {
                        animator.runtimeAnimatorController = controller;
                    }
                }

                return AIToolResult.Succeeded($"Added Animator to {go.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to add Animator: {ex.Message}");
            }
        }

        [AITool("get_animator_info", "Get information about an Animator component")]
        public static AIToolResult GetAnimatorInfo(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                var animator = go.GetComponent<Animator>();
                if (animator == null)
                {
                    return AIToolResult.Failed("GameObject does not have an Animator");
                }

                var controller = animator.runtimeAnimatorController as AnimatorController;
                var parameters = new List<object>();
                var layers = new List<object>();

                if (controller != null)
                {
                    foreach (var param in controller.parameters)
                    {
                        parameters.Add(new
                        {
                            name = param.name,
                            type = param.type.ToString(),
                            defaultValue = param.type switch
                            {
                                AnimatorControllerParameterType.Float => (object)param.defaultFloat,
                                AnimatorControllerParameterType.Int => param.defaultInt,
                                AnimatorControllerParameterType.Bool => param.defaultBool,
                                _ => null
                            }
                        });
                    }

                    foreach (var layer in controller.layers)
                    {
                        var states = new List<string>();
                        foreach (var state in layer.stateMachine.states)
                        {
                            states.Add(state.state.name);
                        }

                        layers.Add(new
                        {
                            name = layer.name,
                            weight = layer.defaultWeight,
                            blendingMode = layer.blendingMode.ToString(),
                            states
                        });
                    }
                }

                var info = new
                {
                    gameObject = go.name,
                    hasController = animator.runtimeAnimatorController != null,
                    controllerName = animator.runtimeAnimatorController?.name,
                    controllerPath = animator.runtimeAnimatorController != null
                        ? AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)
                        : null,
                    applyRootMotion = animator.applyRootMotion,
                    updateMode = animator.updateMode.ToString(),
                    cullingMode = animator.cullingMode.ToString(),
                    parameters,
                    layers,
                    currentState = EditorApplication.isPlaying ? animator.GetCurrentAnimatorStateInfo(0).ToString() : "Not in play mode"
                };

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get Animator info: {ex.Message}");
            }
        }

        [AITool("set_animator_parameter", "Set an Animator parameter value")]
        public static AIToolResult SetAnimatorParameter(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Parameter name")] string parameterName,
            [AIToolParameter("Value (number for float/int, 'true'/'false' for bool, any value for trigger)")] string value)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                var animator = go.GetComponent<Animator>();
                if (animator == null)
                {
                    return AIToolResult.Failed("GameObject does not have an Animator");
                }

                // Find parameter type
                var controller = animator.runtimeAnimatorController as AnimatorController;
                if (controller == null)
                {
                    return AIToolResult.Failed("Animator has no controller");
                }

                var param = controller.parameters.FirstOrDefault(p => p.name == parameterName);
                if (param == null)
                {
                    return AIToolResult.Failed($"Parameter not found: {parameterName}");
                }

                Undo.RecordObject(animator, "Set Animator Parameter");

                switch (param.type)
                {
                    case AnimatorControllerParameterType.Float:
                        animator.SetFloat(parameterName, float.Parse(value));
                        break;
                    case AnimatorControllerParameterType.Int:
                        animator.SetInteger(parameterName, int.Parse(value));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        animator.SetBool(parameterName, bool.Parse(value));
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        animator.SetTrigger(parameterName);
                        break;
                }

                return AIToolResult.Succeeded($"Set {parameterName} = {value}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set parameter: {ex.Message}");
            }
        }

        #endregion

        #region Animator Controller

        [AITool("create_animator_controller", "Create a new Animator Controller asset")]
        public static AIToolResult CreateAnimatorController(
            [AIToolParameter("Path to save the controller (e.g., 'Assets/Animations/MyController.controller')")] string path)
        {
            try
            {
                if (!path.EndsWith(".controller", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".controller";
                }

                // Create directory if needed
                string directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                    AssetDatabase.Refresh();
                }

                var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
                AssetDatabase.SaveAssets();

                return AIToolResult.Succeeded($"Created Animator Controller at {path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to create controller: {ex.Message}");
            }
        }

        [AITool("add_animator_parameter", "Add a parameter to an Animator Controller")]
        public static AIToolResult AddAnimatorParameter(
            [AIToolParameter("Path to the animator controller asset")] string controllerPath,
            [AIToolParameter("Parameter name")] string parameterName,
            [AIToolParameter("Parameter type: 'float', 'int', 'bool', 'trigger'")] string parameterType,
            [AIToolParameter("Default value (for float, int, bool)", isOptional: true)] string defaultValue = null)
        {
            try
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                {
                    return AIToolResult.Failed($"Controller not found: {controllerPath}");
                }

                var type = parameterType.ToLower() switch
                {
                    "float" => AnimatorControllerParameterType.Float,
                    "int" => AnimatorControllerParameterType.Int,
                    "bool" => AnimatorControllerParameterType.Bool,
                    "trigger" => AnimatorControllerParameterType.Trigger,
                    _ => throw new ArgumentException($"Unknown parameter type: {parameterType}")
                };

                Undo.RecordObject(controller, "Add Parameter");
                controller.AddParameter(parameterName, type);

                // Set default value if provided
                if (!string.IsNullOrEmpty(defaultValue))
                {
                    var param = controller.parameters.Last();
                    switch (type)
                    {
                        case AnimatorControllerParameterType.Float:
                            param.defaultFloat = float.Parse(defaultValue);
                            break;
                        case AnimatorControllerParameterType.Int:
                            param.defaultInt = int.Parse(defaultValue);
                            break;
                        case AnimatorControllerParameterType.Bool:
                            param.defaultBool = bool.Parse(defaultValue);
                            break;
                    }
                }

                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return AIToolResult.Succeeded($"Added {parameterType} parameter '{parameterName}'");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to add parameter: {ex.Message}");
            }
        }

        [AITool("add_animator_state", "Add a state to an Animator Controller layer")]
        public static AIToolResult AddAnimatorState(
            [AIToolParameter("Path to the animator controller asset")] string controllerPath,
            [AIToolParameter("State name")] string stateName,
            [AIToolParameter("Animation clip asset path (optional)", isOptional: true)] string clipPath = null,
            [AIToolParameter("Layer index", isOptional: true)] int layerIndex = 0,
            [AIToolParameter("Set as default state", isOptional: true)] bool isDefault = false)
        {
            try
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                {
                    return AIToolResult.Failed($"Controller not found: {controllerPath}");
                }

                if (layerIndex >= controller.layers.Length)
                {
                    return AIToolResult.Failed($"Layer index {layerIndex} out of range");
                }

                var layer = controller.layers[layerIndex];
                var stateMachine = layer.stateMachine;

                Undo.RecordObject(stateMachine, "Add State");
                var state = stateMachine.AddState(stateName);

                if (!string.IsNullOrEmpty(clipPath))
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if (clip != null)
                    {
                        state.motion = clip;
                    }
                }

                if (isDefault)
                {
                    stateMachine.defaultState = state;
                }

                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return AIToolResult.Succeeded($"Added state '{stateName}' to layer {layerIndex}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to add state: {ex.Message}");
            }
        }

        [AITool("add_animator_transition", "Add a transition between animator states")]
        public static AIToolResult AddAnimatorTransition(
            [AIToolParameter("Path to the animator controller asset")] string controllerPath,
            [AIToolParameter("Source state name (or 'AnyState' or 'Entry')")] string sourceState,
            [AIToolParameter("Destination state name")] string destinationState,
            [AIToolParameter("Layer index", isOptional: true)] int layerIndex = 0,
            [AIToolParameter("Has exit time", isOptional: true)] bool hasExitTime = true,
            [AIToolParameter("Exit time (0-1)", isOptional: true)] float exitTime = 0.9f,
            [AIToolParameter("Transition duration", isOptional: true)] float duration = 0.25f)
        {
            try
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                {
                    return AIToolResult.Failed($"Controller not found: {controllerPath}");
                }

                if (layerIndex >= controller.layers.Length)
                {
                    return AIToolResult.Failed($"Layer index {layerIndex} out of range");
                }

                var stateMachine = controller.layers[layerIndex].stateMachine;

                var destState = stateMachine.states.FirstOrDefault(s => s.state.name == destinationState).state;
                if (destState == null)
                {
                    return AIToolResult.Failed($"Destination state not found: {destinationState}");
                }

                AnimatorStateTransition transition;

                if (sourceState.Equals("AnyState", StringComparison.OrdinalIgnoreCase))
                {
                    Undo.RecordObject(stateMachine, "Add AnyState Transition");
                    transition = stateMachine.AddAnyStateTransition(destState);
                }
                else if (sourceState.Equals("Entry", StringComparison.OrdinalIgnoreCase))
                {
                    Undo.RecordObject(stateMachine, "Add Entry Transition");
                    stateMachine.defaultState = destState;
                    return AIToolResult.Succeeded($"Set '{destinationState}' as default state");
                }
                else
                {
                    var srcState = stateMachine.states.FirstOrDefault(s => s.state.name == sourceState).state;
                    if (srcState == null)
                    {
                        return AIToolResult.Failed($"Source state not found: {sourceState}");
                    }

                    Undo.RecordObject(srcState, "Add Transition");
                    transition = srcState.AddTransition(destState);
                }

                transition.hasExitTime = hasExitTime;
                transition.exitTime = exitTime;
                transition.duration = duration;

                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return AIToolResult.Succeeded($"Added transition from '{sourceState}' to '{destinationState}'");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to add transition: {ex.Message}");
            }
        }

        [AITool("add_transition_condition", "Add a condition to an animator transition")]
        public static AIToolResult AddTransitionCondition(
            [AIToolParameter("Path to the animator controller asset")] string controllerPath,
            [AIToolParameter("Source state name")] string sourceState,
            [AIToolParameter("Destination state name")] string destinationState,
            [AIToolParameter("Parameter name")] string parameterName,
            [AIToolParameter("Condition mode: 'if', 'ifNot', 'greater', 'less', 'equals', 'notEquals'")] string conditionMode,
            [AIToolParameter("Threshold value (for greater/less/equals)", isOptional: true)] float threshold = 0,
            [AIToolParameter("Layer index", isOptional: true)] int layerIndex = 0)
        {
            try
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                {
                    return AIToolResult.Failed($"Controller not found: {controllerPath}");
                }

                var stateMachine = controller.layers[layerIndex].stateMachine;

                AnimatorStateTransition transition = null;

                if (sourceState.Equals("AnyState", StringComparison.OrdinalIgnoreCase))
                {
                    transition = stateMachine.anyStateTransitions
                        .FirstOrDefault(t => t.destinationState?.name == destinationState);
                }
                else
                {
                    var srcState = stateMachine.states.FirstOrDefault(s => s.state.name == sourceState).state;
                    if (srcState != null)
                    {
                        transition = srcState.transitions
                            .FirstOrDefault(t => t.destinationState?.name == destinationState);
                    }
                }

                if (transition == null)
                {
                    return AIToolResult.Failed($"Transition from '{sourceState}' to '{destinationState}' not found");
                }

                var mode = conditionMode.ToLower() switch
                {
                    "if" => AnimatorConditionMode.If,
                    "ifnot" => AnimatorConditionMode.IfNot,
                    "greater" => AnimatorConditionMode.Greater,
                    "less" => AnimatorConditionMode.Less,
                    "equals" => AnimatorConditionMode.Equals,
                    "notequals" => AnimatorConditionMode.NotEqual,
                    _ => throw new ArgumentException($"Unknown condition mode: {conditionMode}")
                };

                Undo.RecordObject(transition, "Add Condition");
                transition.AddCondition(mode, threshold, parameterName);

                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return AIToolResult.Succeeded($"Added condition: {parameterName} {conditionMode} {threshold}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to add condition: {ex.Message}");
            }
        }

        [AITool("add_animator_layer", "Add a layer to an Animator Controller")]
        public static AIToolResult AddAnimatorLayer(
            [AIToolParameter("Path to the animator controller asset")] string controllerPath,
            [AIToolParameter("Layer name")] string layerName,
            [AIToolParameter("Default weight", isOptional: true)] float weight = 1f,
            [AIToolParameter("Blending mode: 'override' or 'additive'", isOptional: true)] string blendingMode = "override")
        {
            try
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                {
                    return AIToolResult.Failed($"Controller not found: {controllerPath}");
                }

                Undo.RecordObject(controller, "Add Layer");

                var layer = new AnimatorControllerLayer
                {
                    name = layerName,
                    defaultWeight = weight,
                    blendingMode = blendingMode.ToLower() == "additive"
                        ? AnimatorLayerBlendingMode.Additive
                        : AnimatorLayerBlendingMode.Override,
                    stateMachine = new AnimatorStateMachine()
                };

                layer.stateMachine.name = layerName;
                layer.stateMachine.hideFlags = HideFlags.HideInHierarchy;

                AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);

                var layers = controller.layers.ToList();
                layers.Add(layer);
                controller.layers = layers.ToArray();

                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return AIToolResult.Succeeded($"Added layer '{layerName}'");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to add layer: {ex.Message}");
            }
        }

        #endregion

        #region Animation Clips

        [AITool("create_animation_clip", "Create a new animation clip asset")]
        public static AIToolResult CreateAnimationClip(
            [AIToolParameter("Path to save the clip (e.g., 'Assets/Animations/MyClip.anim')")] string path,
            [AIToolParameter("Clip name", isOptional: true)] string name = null,
            [AIToolParameter("Loop the animation", isOptional: true)] bool loop = false)
        {
            try
            {
                if (!path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".anim";
                }

                // Create directory if needed
                string directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                    AssetDatabase.Refresh();
                }

                var clip = new AnimationClip();
                clip.name = name ?? System.IO.Path.GetFileNameWithoutExtension(path);

                if (loop)
                {
                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.loopTime = true;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                }

                AssetDatabase.CreateAsset(clip, path);
                AssetDatabase.SaveAssets();

                return AIToolResult.Succeeded($"Created animation clip at {path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to create clip: {ex.Message}");
            }
        }

        [AITool("get_animation_clip_info", "Get information about an animation clip")]
        public static AIToolResult GetAnimationClipInfo(
            [AIToolParameter("Path to the animation clip asset")] string clipPath)
        {
            try
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    return AIToolResult.Failed($"Animation clip not found: {clipPath}");
                }

                var bindings = AnimationUtility.GetCurveBindings(clip);
                var curves = bindings.Select(b => new
                {
                    path = b.path,
                    type = b.type.Name,
                    propertyName = b.propertyName
                }).ToArray();

                var events = AnimationUtility.GetAnimationEvents(clip);
                var eventInfos = events.Select(e => new
                {
                    time = e.time,
                    functionName = e.functionName,
                    stringParameter = e.stringParameter,
                    floatParameter = e.floatParameter,
                    intParameter = e.intParameter
                }).ToArray();

                var settings = AnimationUtility.GetAnimationClipSettings(clip);

                var info = new
                {
                    name = clip.name,
                    path = clipPath,
                    length = clip.length,
                    frameRate = clip.frameRate,
                    isLooping = settings.loopTime,
                    wrapMode = clip.wrapMode.ToString(),
                    isHumanMotion = clip.humanMotion,
                    isLegacy = clip.legacy,
                    curveCount = bindings.Length,
                    curves,
                    eventCount = events.Length,
                    events = eventInfos
                };

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get clip info: {ex.Message}");
            }
        }

        [AITool("add_animation_curve", "Add an animation curve to a clip")]
        public static AIToolResult AddAnimationCurve(
            [AIToolParameter("Path to the animation clip asset")] string clipPath,
            [AIToolParameter("Property path (e.g., 'localPosition.x')")] string propertyName,
            [AIToolParameter("Target type (e.g., 'Transform')")] string targetType,
            [AIToolParameter("Keyframes as JSON array: [{time: 0, value: 0}, {time: 1, value: 10}]")] string keyframesJson,
            [AIToolParameter("Relative path to target object (empty for root)", isOptional: true)] string relativePath = "")
        {
            try
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    return AIToolResult.Failed($"Animation clip not found: {clipPath}");
                }

                var type = FindType(targetType);
                if (type == null)
                {
                    return AIToolResult.Failed($"Type not found: {targetType}");
                }

                var keyframeData = JsonConvert.DeserializeObject<KeyframeData[]>(keyframesJson);
                var keyframes = keyframeData.Select(k => new Keyframe(k.time, k.value)).ToArray();
                var curve = new AnimationCurve(keyframes);

                var binding = EditorCurveBinding.FloatCurve(relativePath, type, propertyName);

                Undo.RecordObject(clip, "Add Animation Curve");
                AnimationUtility.SetEditorCurve(clip, binding, curve);

                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();

                return AIToolResult.Succeeded($"Added curve for {propertyName} with {keyframes.Length} keyframes");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to add curve: {ex.Message}");
            }
        }

        [AITool("add_animation_event", "Add an animation event to a clip")]
        public static AIToolResult AddAnimationEvent(
            [AIToolParameter("Path to the animation clip asset")] string clipPath,
            [AIToolParameter("Time of the event (in seconds)")] float time,
            [AIToolParameter("Function name to call")] string functionName,
            [AIToolParameter("Float parameter", isOptional: true)] float floatParam = 0,
            [AIToolParameter("Int parameter", isOptional: true)] int intParam = 0,
            [AIToolParameter("String parameter", isOptional: true)] string stringParam = "")
        {
            try
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    return AIToolResult.Failed($"Animation clip not found: {clipPath}");
                }

                var events = AnimationUtility.GetAnimationEvents(clip).ToList();
                events.Add(new AnimationEvent
                {
                    time = time,
                    functionName = functionName,
                    floatParameter = floatParam,
                    intParameter = intParam,
                    stringParameter = stringParam
                });

                Undo.RecordObject(clip, "Add Animation Event");
                AnimationUtility.SetAnimationEvents(clip, events.ToArray());

                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();

                return AIToolResult.Succeeded($"Added animation event '{functionName}' at {time}s");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to add event: {ex.Message}");
            }
        }

        [AITool("set_animation_clip_settings", "Configure animation clip settings")]
        public static AIToolResult SetAnimationClipSettings(
            [AIToolParameter("Path to the animation clip asset")] string clipPath,
            [AIToolParameter("Loop time", isOptional: true)] bool? loopTime = null,
            [AIToolParameter("Loop pose", isOptional: true)] bool? loopPose = null,
            [AIToolParameter("Cycle offset", isOptional: true)] float? cycleOffset = null,
            [AIToolParameter("Mirror", isOptional: true)] bool? mirror = null)
        {
            try
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    return AIToolResult.Failed($"Animation clip not found: {clipPath}");
                }

                Undo.RecordObject(clip, "Set Animation Settings");

                var settings = AnimationUtility.GetAnimationClipSettings(clip);

                if (loopTime.HasValue) settings.loopTime = loopTime.Value;
                if (loopPose.HasValue) settings.loopBlend = loopPose.Value;
                if (cycleOffset.HasValue) settings.cycleOffset = cycleOffset.Value;
                if (mirror.HasValue) settings.mirror = mirror.Value;

                AnimationUtility.SetAnimationClipSettings(clip, settings);

                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();

                return AIToolResult.Succeeded($"Updated settings for {clip.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set settings: {ex.Message}");
            }
        }

        #endregion

        #region Search & List

        [AITool("list_animation_clips", "List all animation clips in a folder")]
        public static AIToolResult ListAnimationClips(
            [AIToolParameter("Folder to search (e.g., 'Assets/Animations')", isOptional: true)] string folder = "Assets")
        {
            try
            {
                var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folder });
                var clips = guids.Select(guid =>
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    return new
                    {
                        name = clip?.name ?? System.IO.Path.GetFileNameWithoutExtension(path),
                        path,
                        length = clip?.length ?? 0,
                        isLooping = clip != null && AnimationUtility.GetAnimationClipSettings(clip).loopTime
                    };
                }).ToArray();

                return AIToolResult.Succeeded($"Found {clips.Length} animation clips", new { clips });
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to list clips: {ex.Message}");
            }
        }

        [AITool("list_animator_controllers", "List all animator controllers in a folder")]
        public static AIToolResult ListAnimatorControllers(
            [AIToolParameter("Folder to search (e.g., 'Assets/Animations')", isOptional: true)] string folder = "Assets")
        {
            try
            {
                var guids = AssetDatabase.FindAssets("t:AnimatorController", new[] { folder });
                var controllers = guids.Select(guid =>
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                    return new
                    {
                        name = controller?.name ?? System.IO.Path.GetFileNameWithoutExtension(path),
                        path,
                        layerCount = controller?.layers.Length ?? 0,
                        parameterCount = controller?.parameters.Length ?? 0
                    };
                }).ToArray();

                return AIToolResult.Succeeded($"Found {controllers.Length} animator controllers", new { controllers });
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to list controllers: {ex.Message}");
            }
        }

        [AITool("find_objects_with_animator", "Find all GameObjects with an Animator component")]
        public static AIToolResult FindObjectsWithAnimator(
            [AIToolParameter("Include inactive objects", isOptional: true)] bool includeInactive = false)
        {
            try
            {
                var animators = UnityEngine.Object.FindObjectsByType<Animator>(
                    includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

                var results = animators.Select(a => new
                {
                    gameObject = a.gameObject.name,
                    path = GetGameObjectPath(a.gameObject),
                    hasController = a.runtimeAnimatorController != null,
                    controllerName = a.runtimeAnimatorController?.name
                }).ToArray();

                return AIToolResult.Succeeded($"Found {results.Length} objects with Animator", new { results });
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Search failed: {ex.Message}");
            }
        }

        #endregion

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

            type = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null) return type;

            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
        }

        private class KeyframeData
        {
            public float time { get; set; }
            public float value { get; set; }
        }

        #endregion
    }
}
