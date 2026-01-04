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
    /// AI tools for physics configuration and testing.
    /// </summary>
    public static class PhysicsTools
    {
        #region Rigidbody

        [AITool("add_rigidbody", "Add a Rigidbody component to a GameObject")]
        public static AIToolResult AddRigidbody(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Mass", isOptional: true)] float mass = 1f,
            [AIToolParameter("Use gravity", isOptional: true)] bool useGravity = true,
            [AIToolParameter("Is kinematic", isOptional: true)] bool isKinematic = false,
            [AIToolParameter("Use 2D physics instead", isOptional: true)] bool is2D = false)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                if (is2D)
                {
                    if (go.GetComponent<Rigidbody2D>() != null)
                    {
                        return AIToolResult.Failed("GameObject already has Rigidbody2D");
                    }

                    var rb2d = Undo.AddComponent<Rigidbody2D>(go);
                    rb2d.mass = mass;
                    rb2d.gravityScale = useGravity ? 1f : 0f;
                    rb2d.bodyType = isKinematic ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;

                    return AIToolResult.Succeeded($"Added Rigidbody2D to {go.name}");
                }
                else
                {
                    if (go.GetComponent<Rigidbody>() != null)
                    {
                        return AIToolResult.Failed("GameObject already has Rigidbody");
                    }

                    var rb = Undo.AddComponent<Rigidbody>(go);
                    rb.mass = mass;
                    rb.useGravity = useGravity;
                    rb.isKinematic = isKinematic;

                    return AIToolResult.Succeeded($"Added Rigidbody to {go.name}");
                }
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to add Rigidbody: {ex.Message}");
            }
        }

        [AITool("configure_rigidbody", "Configure a Rigidbody's properties")]
        public static AIToolResult ConfigureRigidbody(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Mass", isOptional: true)] float? mass = null,
            [AIToolParameter("Drag", isOptional: true)] float? drag = null,
            [AIToolParameter("Angular drag", isOptional: true)] float? angularDrag = null,
            [AIToolParameter("Use gravity", isOptional: true)] bool? useGravity = null,
            [AIToolParameter("Is kinematic", isOptional: true)] bool? isKinematic = null,
            [AIToolParameter("Interpolation: 'none', 'interpolate', 'extrapolate'", isOptional: true)] string interpolation = null,
            [AIToolParameter("Collision detection: 'discrete', 'continuous', 'continuousDynamic', 'continuousSpeculative'", isOptional: true)] string collisionDetection = null,
            [AIToolParameter("Freeze position axes (comma-separated: 'x,y,z')", isOptional: true)] string freezePosition = null,
            [AIToolParameter("Freeze rotation axes (comma-separated: 'x,y,z')", isOptional: true)] string freezeRotation = null)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                var rb = go.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    return AIToolResult.Failed("GameObject does not have a Rigidbody");
                }

                Undo.RecordObject(rb, "Configure Rigidbody");

                if (mass.HasValue) rb.mass = mass.Value;
                if (drag.HasValue) rb.linearDamping = drag.Value;
                if (angularDrag.HasValue) rb.angularDamping = angularDrag.Value;
                if (useGravity.HasValue) rb.useGravity = useGravity.Value;
                if (isKinematic.HasValue) rb.isKinematic = isKinematic.Value;

                if (!string.IsNullOrEmpty(interpolation))
                {
                    rb.interpolation = interpolation.ToLower() switch
                    {
                        "interpolate" => RigidbodyInterpolation.Interpolate,
                        "extrapolate" => RigidbodyInterpolation.Extrapolate,
                        _ => RigidbodyInterpolation.None
                    };
                }

                if (!string.IsNullOrEmpty(collisionDetection))
                {
                    rb.collisionDetectionMode = collisionDetection.ToLower() switch
                    {
                        "continuous" => CollisionDetectionMode.Continuous,
                        "continuousdynamic" => CollisionDetectionMode.ContinuousDynamic,
                        "continuousspeculative" => CollisionDetectionMode.ContinuousSpeculative,
                        _ => CollisionDetectionMode.Discrete
                    };
                }

                if (!string.IsNullOrEmpty(freezePosition) || !string.IsNullOrEmpty(freezeRotation))
                {
                    var constraints = RigidbodyConstraints.None;

                    if (!string.IsNullOrEmpty(freezePosition))
                    {
                        var axes = freezePosition.ToLower().Split(',').Select(s => s.Trim());
                        if (axes.Contains("x")) constraints |= RigidbodyConstraints.FreezePositionX;
                        if (axes.Contains("y")) constraints |= RigidbodyConstraints.FreezePositionY;
                        if (axes.Contains("z")) constraints |= RigidbodyConstraints.FreezePositionZ;
                    }

                    if (!string.IsNullOrEmpty(freezeRotation))
                    {
                        var axes = freezeRotation.ToLower().Split(',').Select(s => s.Trim());
                        if (axes.Contains("x")) constraints |= RigidbodyConstraints.FreezeRotationX;
                        if (axes.Contains("y")) constraints |= RigidbodyConstraints.FreezeRotationY;
                        if (axes.Contains("z")) constraints |= RigidbodyConstraints.FreezeRotationZ;
                    }

                    rb.constraints = constraints;
                }

                return AIToolResult.Succeeded($"Configured Rigidbody on {go.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to configure Rigidbody: {ex.Message}");
            }
        }

        [AITool("get_rigidbody_info", "Get Rigidbody information from a GameObject")]
        public static AIToolResult GetRigidbodyInfo(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                var rb = go.GetComponent<Rigidbody>();
                var rb2d = go.GetComponent<Rigidbody2D>();

                if (rb != null)
                {
                    var info = new
                    {
                        type = "Rigidbody",
                        mass = rb.mass,
                        drag = rb.linearDamping,
                        angularDrag = rb.angularDamping,
                        useGravity = rb.useGravity,
                        isKinematic = rb.isKinematic,
                        interpolation = rb.interpolation.ToString(),
                        collisionDetectionMode = rb.collisionDetectionMode.ToString(),
                        constraints = rb.constraints.ToString(),
                        velocity = new { x = rb.linearVelocity.x, y = rb.linearVelocity.y, z = rb.linearVelocity.z },
                        angularVelocity = new { x = rb.angularVelocity.x, y = rb.angularVelocity.y, z = rb.angularVelocity.z }
                    };
                    return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
                }
                else if (rb2d != null)
                {
                    var info = new
                    {
                        type = "Rigidbody2D",
                        mass = rb2d.mass,
                        linearDamping = rb2d.linearDamping,
                        angularDamping = rb2d.angularDamping,
                        gravityScale = rb2d.gravityScale,
                        bodyType = rb2d.bodyType.ToString(),
                        interpolation = rb2d.interpolation.ToString(),
                        collisionDetectionMode = rb2d.collisionDetectionMode.ToString(),
                        constraints = rb2d.constraints.ToString(),
                        velocity = new { x = rb2d.linearVelocity.x, y = rb2d.linearVelocity.y },
                        angularVelocity = rb2d.angularVelocity
                    };
                    return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
                }
                else
                {
                    return AIToolResult.Failed("GameObject does not have a Rigidbody or Rigidbody2D");
                }
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get Rigidbody info: {ex.Message}");
            }
        }

        #endregion

        #region Colliders

        [AITool("add_collider", "Add a collider to a GameObject")]
        public static AIToolResult AddCollider(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Collider type: 'box', 'sphere', 'capsule', 'mesh', 'box2d', 'circle2d', 'polygon2d', 'capsule2d'")] string colliderType,
            [AIToolParameter("Is trigger", isOptional: true)] bool isTrigger = false)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                Collider collider = null;
                Collider2D collider2D = null;

                switch (colliderType.ToLower())
                {
                    case "box":
                        collider = Undo.AddComponent<BoxCollider>(go);
                        break;
                    case "sphere":
                        collider = Undo.AddComponent<SphereCollider>(go);
                        break;
                    case "capsule":
                        collider = Undo.AddComponent<CapsuleCollider>(go);
                        break;
                    case "mesh":
                        collider = Undo.AddComponent<MeshCollider>(go);
                        break;
                    case "box2d":
                        collider2D = Undo.AddComponent<BoxCollider2D>(go);
                        break;
                    case "circle2d":
                        collider2D = Undo.AddComponent<CircleCollider2D>(go);
                        break;
                    case "polygon2d":
                        collider2D = Undo.AddComponent<PolygonCollider2D>(go);
                        break;
                    case "capsule2d":
                        collider2D = Undo.AddComponent<CapsuleCollider2D>(go);
                        break;
                    default:
                        return AIToolResult.Failed($"Unknown collider type: {colliderType}");
                }

                if (collider != null)
                {
                    collider.isTrigger = isTrigger;
                    return AIToolResult.Succeeded($"Added {colliderType} collider to {go.name}");
                }
                else if (collider2D != null)
                {
                    collider2D.isTrigger = isTrigger;
                    return AIToolResult.Succeeded($"Added {colliderType} collider to {go.name}");
                }

                return AIToolResult.Failed("Failed to add collider");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to add collider: {ex.Message}");
            }
        }

        [AITool("configure_box_collider", "Configure a BoxCollider's properties")]
        public static AIToolResult ConfigureBoxCollider(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Center X", isOptional: true)] float? centerX = null,
            [AIToolParameter("Center Y", isOptional: true)] float? centerY = null,
            [AIToolParameter("Center Z", isOptional: true)] float? centerZ = null,
            [AIToolParameter("Size X", isOptional: true)] float? sizeX = null,
            [AIToolParameter("Size Y", isOptional: true)] float? sizeY = null,
            [AIToolParameter("Size Z", isOptional: true)] float? sizeZ = null,
            [AIToolParameter("Is trigger", isOptional: true)] bool? isTrigger = null)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                var collider = go.GetComponent<BoxCollider>();
                if (collider == null)
                {
                    return AIToolResult.Failed("GameObject does not have a BoxCollider");
                }

                Undo.RecordObject(collider, "Configure BoxCollider");

                var center = collider.center;
                if (centerX.HasValue) center.x = centerX.Value;
                if (centerY.HasValue) center.y = centerY.Value;
                if (centerZ.HasValue) center.z = centerZ.Value;
                collider.center = center;

                var size = collider.size;
                if (sizeX.HasValue) size.x = sizeX.Value;
                if (sizeY.HasValue) size.y = sizeY.Value;
                if (sizeZ.HasValue) size.z = sizeZ.Value;
                collider.size = size;

                if (isTrigger.HasValue) collider.isTrigger = isTrigger.Value;

                return AIToolResult.Succeeded($"Configured BoxCollider on {go.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to configure BoxCollider: {ex.Message}");
            }
        }

        [AITool("configure_sphere_collider", "Configure a SphereCollider's properties")]
        public static AIToolResult ConfigureSphereCollider(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Center X", isOptional: true)] float? centerX = null,
            [AIToolParameter("Center Y", isOptional: true)] float? centerY = null,
            [AIToolParameter("Center Z", isOptional: true)] float? centerZ = null,
            [AIToolParameter("Radius", isOptional: true)] float? radius = null,
            [AIToolParameter("Is trigger", isOptional: true)] bool? isTrigger = null)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                var collider = go.GetComponent<SphereCollider>();
                if (collider == null)
                {
                    return AIToolResult.Failed("GameObject does not have a SphereCollider");
                }

                Undo.RecordObject(collider, "Configure SphereCollider");

                var center = collider.center;
                if (centerX.HasValue) center.x = centerX.Value;
                if (centerY.HasValue) center.y = centerY.Value;
                if (centerZ.HasValue) center.z = centerZ.Value;
                collider.center = center;

                if (radius.HasValue) collider.radius = radius.Value;
                if (isTrigger.HasValue) collider.isTrigger = isTrigger.Value;

                return AIToolResult.Succeeded($"Configured SphereCollider on {go.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to configure SphereCollider: {ex.Message}");
            }
        }

        [AITool("get_colliders", "Get all colliders on a GameObject")]
        public static AIToolResult GetColliders(
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

                Collider[] colliders3D = includeChildren
                    ? go.GetComponentsInChildren<Collider>(true)
                    : go.GetComponents<Collider>();

                Collider2D[] colliders2D = includeChildren
                    ? go.GetComponentsInChildren<Collider2D>(true)
                    : go.GetComponents<Collider2D>();

                var colliderInfos = new List<object>();

                foreach (var col in colliders3D)
                {
                    var info = new
                    {
                        gameObject = col.gameObject.name,
                        type = col.GetType().Name,
                        isTrigger = col.isTrigger,
                        enabled = col.enabled,
                        bounds = new
                        {
                            center = new { x = col.bounds.center.x, y = col.bounds.center.y, z = col.bounds.center.z },
                            size = new { x = col.bounds.size.x, y = col.bounds.size.y, z = col.bounds.size.z }
                        }
                    };
                    colliderInfos.Add(info);
                }

                foreach (var col in colliders2D)
                {
                    var info = new
                    {
                        gameObject = col.gameObject.name,
                        type = col.GetType().Name,
                        isTrigger = col.isTrigger,
                        enabled = col.enabled,
                        bounds = new
                        {
                            center = new { x = col.bounds.center.x, y = col.bounds.center.y },
                            size = new { x = col.bounds.size.x, y = col.bounds.size.y }
                        }
                    };
                    colliderInfos.Add(info);
                }

                return AIToolResult.Succeeded($"Found {colliderInfos.Count} colliders", new { colliders = colliderInfos });
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get colliders: {ex.Message}");
            }
        }

        #endregion

        #region Raycasting

        [AITool("raycast", "Perform a raycast in the scene (editor mode)")]
        public static AIToolResult Raycast(
            [AIToolParameter("Origin X")] float originX,
            [AIToolParameter("Origin Y")] float originY,
            [AIToolParameter("Origin Z")] float originZ,
            [AIToolParameter("Direction X")] float dirX,
            [AIToolParameter("Direction Y")] float dirY,
            [AIToolParameter("Direction Z")] float dirZ,
            [AIToolParameter("Maximum distance", isOptional: true)] float maxDistance = Mathf.Infinity,
            [AIToolParameter("Layer mask (layer names comma-separated)", isOptional: true)] string layerMask = "")
        {
            try
            {
                var origin = new Vector3(originX, originY, originZ);
                var direction = new Vector3(dirX, dirY, dirZ).normalized;

                int mask = string.IsNullOrEmpty(layerMask)
                    ? Physics.DefaultRaycastLayers
                    : GetLayerMask(layerMask);

                if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, mask))
                {
                    var info = new
                    {
                        hit = true,
                        gameObject = hit.collider.gameObject.name,
                        path = GetGameObjectPath(hit.collider.gameObject),
                        point = new { x = hit.point.x, y = hit.point.y, z = hit.point.z },
                        normal = new { x = hit.normal.x, y = hit.normal.y, z = hit.normal.z },
                        distance = hit.distance,
                        colliderType = hit.collider.GetType().Name
                    };
                    return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
                }
                else
                {
                    return AIToolResult.Succeeded(JsonConvert.SerializeObject(new { hit = false }, Formatting.Indented));
                }
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Raycast failed: {ex.Message}");
            }
        }

        [AITool("raycast_all", "Perform a raycast that returns all hits")]
        public static AIToolResult RaycastAll(
            [AIToolParameter("Origin X")] float originX,
            [AIToolParameter("Origin Y")] float originY,
            [AIToolParameter("Origin Z")] float originZ,
            [AIToolParameter("Direction X")] float dirX,
            [AIToolParameter("Direction Y")] float dirY,
            [AIToolParameter("Direction Z")] float dirZ,
            [AIToolParameter("Maximum distance", isOptional: true)] float maxDistance = Mathf.Infinity,
            [AIToolParameter("Layer mask (layer names comma-separated)", isOptional: true)] string layerMask = "")
        {
            try
            {
                var origin = new Vector3(originX, originY, originZ);
                var direction = new Vector3(dirX, dirY, dirZ).normalized;

                int mask = string.IsNullOrEmpty(layerMask)
                    ? Physics.DefaultRaycastLayers
                    : GetLayerMask(layerMask);

                var hits = Physics.RaycastAll(origin, direction, maxDistance, mask);

                var hitInfos = hits.OrderBy(h => h.distance).Select(hit => new
                {
                    gameObject = hit.collider.gameObject.name,
                    path = GetGameObjectPath(hit.collider.gameObject),
                    point = new { x = hit.point.x, y = hit.point.y, z = hit.point.z },
                    normal = new { x = hit.normal.x, y = hit.normal.y, z = hit.normal.z },
                    distance = hit.distance,
                    colliderType = hit.collider.GetType().Name
                }).ToArray();

                return AIToolResult.Succeeded($"Found {hitInfos.Length} hits", new { hits = hitInfos });
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"RaycastAll failed: {ex.Message}");
            }
        }

        [AITool("overlap_sphere", "Find all colliders within a sphere")]
        public static AIToolResult OverlapSphere(
            [AIToolParameter("Center X")] float centerX,
            [AIToolParameter("Center Y")] float centerY,
            [AIToolParameter("Center Z")] float centerZ,
            [AIToolParameter("Radius")] float radius,
            [AIToolParameter("Layer mask (layer names comma-separated)", isOptional: true)] string layerMask = "")
        {
            try
            {
                var center = new Vector3(centerX, centerY, centerZ);

                int mask = string.IsNullOrEmpty(layerMask)
                    ? Physics.DefaultRaycastLayers
                    : GetLayerMask(layerMask);

                var colliders = Physics.OverlapSphere(center, radius, mask);

                var colliderInfos = colliders.Select(col => new
                {
                    gameObject = col.gameObject.name,
                    path = GetGameObjectPath(col.gameObject),
                    colliderType = col.GetType().Name,
                    distance = Vector3.Distance(center, col.bounds.center)
                }).OrderBy(c => c.distance).ToArray();

                return AIToolResult.Succeeded($"Found {colliderInfos.Length} colliders", new { colliders = colliderInfos });
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"OverlapSphere failed: {ex.Message}");
            }
        }

        [AITool("overlap_box", "Find all colliders within a box")]
        public static AIToolResult OverlapBox(
            [AIToolParameter("Center X")] float centerX,
            [AIToolParameter("Center Y")] float centerY,
            [AIToolParameter("Center Z")] float centerZ,
            [AIToolParameter("Half extents X")] float halfExtentX,
            [AIToolParameter("Half extents Y")] float halfExtentY,
            [AIToolParameter("Half extents Z")] float halfExtentZ,
            [AIToolParameter("Layer mask (layer names comma-separated)", isOptional: true)] string layerMask = "")
        {
            try
            {
                var center = new Vector3(centerX, centerY, centerZ);
                var halfExtents = new Vector3(halfExtentX, halfExtentY, halfExtentZ);

                int mask = string.IsNullOrEmpty(layerMask)
                    ? Physics.DefaultRaycastLayers
                    : GetLayerMask(layerMask);

                var colliders = Physics.OverlapBox(center, halfExtents, Quaternion.identity, mask);

                var colliderInfos = colliders.Select(col => new
                {
                    gameObject = col.gameObject.name,
                    path = GetGameObjectPath(col.gameObject),
                    colliderType = col.GetType().Name
                }).ToArray();

                return AIToolResult.Succeeded($"Found {colliderInfos.Length} colliders", new { colliders = colliderInfos });
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"OverlapBox failed: {ex.Message}");
            }
        }

        #endregion

        #region Physics Settings

        [AITool("get_physics_settings", "Get global physics settings")]
        public static AIToolResult GetPhysicsSettings()
        {
            var info = new
            {
                gravity = new { x = Physics.gravity.x, y = Physics.gravity.y, z = Physics.gravity.z },
                defaultContactOffset = Physics.defaultContactOffset,
                bounceThreshold = Physics.bounceThreshold,
                defaultSolverIterations = Physics.defaultSolverIterations,
                defaultSolverVelocityIterations = Physics.defaultSolverVelocityIterations,
                queriesHitTriggers = Physics.queriesHitTriggers,
                queriesHitBackfaces = Physics.queriesHitBackfaces,
                autoSimulation = Physics.simulationMode != SimulationMode.Script,
                autoSyncTransforms = Physics.autoSyncTransforms
            };

            return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
        }

        [AITool("set_gravity", "Set the global gravity", requiresConfirmation: true)]
        public static AIToolResult SetGravity(
            [AIToolParameter("Gravity X")] float x,
            [AIToolParameter("Gravity Y")] float y,
            [AIToolParameter("Gravity Z")] float z)
        {
            Physics.gravity = new Vector3(x, y, z);
            return AIToolResult.Succeeded($"Set gravity to ({x}, {y}, {z})");
        }

        [AITool("list_layers", "List all physics layers")]
        public static AIToolResult ListLayers()
        {
            var layers = new List<object>();
            for (int i = 0; i < 32; i++)
            {
                var name = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(name))
                {
                    layers.Add(new { index = i, name });
                }
            }

            return AIToolResult.Succeeded(JsonConvert.SerializeObject(new { layers }, Formatting.Indented));
        }

        [AITool("get_layer_collision_matrix", "Get which layers collide with each other")]
        public static AIToolResult GetLayerCollisionMatrix(
            [AIToolParameter("Layer name or index")] string layer)
        {
            try
            {
                int layerIndex;
                if (!int.TryParse(layer, out layerIndex))
                {
                    layerIndex = LayerMask.NameToLayer(layer);
                    if (layerIndex < 0)
                    {
                        return AIToolResult.Failed($"Layer not found: {layer}");
                    }
                }

                var collidingLayers = new List<object>();
                for (int i = 0; i < 32; i++)
                {
                    var name = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(name))
                    {
                        bool collides = !Physics.GetIgnoreLayerCollision(layerIndex, i);
                        collidingLayers.Add(new { index = i, name, collides });
                    }
                }

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(new
                {
                    layer = LayerMask.LayerToName(layerIndex),
                    layerIndex,
                    collisionMatrix = collidingLayers
                }, Formatting.Indented));
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get collision matrix: {ex.Message}");
            }
        }

        [AITool("set_layer_collision", "Set whether two layers collide", requiresConfirmation: true)]
        public static AIToolResult SetLayerCollision(
            [AIToolParameter("First layer name or index")] string layer1,
            [AIToolParameter("Second layer name or index")] string layer2,
            [AIToolParameter("Should collide")] bool collide)
        {
            try
            {
                int layer1Index = int.TryParse(layer1, out int l1) ? l1 : LayerMask.NameToLayer(layer1);
                int layer2Index = int.TryParse(layer2, out int l2) ? l2 : LayerMask.NameToLayer(layer2);

                if (layer1Index < 0 || layer2Index < 0)
                {
                    return AIToolResult.Failed("One or both layers not found");
                }

                Physics.IgnoreLayerCollision(layer1Index, layer2Index, !collide);
                return AIToolResult.Succeeded($"Set collision between {LayerMask.LayerToName(layer1Index)} and {LayerMask.LayerToName(layer2Index)} to {collide}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to set layer collision: {ex.Message}");
            }
        }

        #endregion

        #region Joints

        [AITool("add_joint", "Add a physics joint to a GameObject")]
        public static AIToolResult AddJoint(
            [AIToolParameter("Path or name of the GameObject")] string gameObjectPath,
            [AIToolParameter("Joint type: 'fixed', 'hinge', 'spring', 'configurable', 'character'")] string jointType,
            [AIToolParameter("Connected body GameObject path (optional)", isOptional: true)] string connectedBodyPath = null)
        {
            try
            {
                var go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return AIToolResult.Failed($"GameObject not found: {gameObjectPath}");
                }

                Rigidbody connectedBody = null;
                if (!string.IsNullOrEmpty(connectedBodyPath))
                {
                    var connectedGo = GameObject.Find(connectedBodyPath);
                    if (connectedGo != null)
                    {
                        connectedBody = connectedGo.GetComponent<Rigidbody>();
                    }
                }

                Joint joint = jointType.ToLower() switch
                {
                    "fixed" => Undo.AddComponent<FixedJoint>(go),
                    "hinge" => Undo.AddComponent<HingeJoint>(go),
                    "spring" => Undo.AddComponent<SpringJoint>(go),
                    "configurable" => Undo.AddComponent<ConfigurableJoint>(go),
                    "character" => Undo.AddComponent<CharacterJoint>(go),
                    _ => null
                };

                if (joint == null)
                {
                    return AIToolResult.Failed($"Unknown joint type: {jointType}");
                }

                if (connectedBody != null)
                {
                    joint.connectedBody = connectedBody;
                }

                return AIToolResult.Succeeded($"Added {jointType} joint to {go.name}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to add joint: {ex.Message}");
            }
        }

        #endregion

        #region Helpers

        private static int GetLayerMask(string layerNames)
        {
            var names = layerNames.Split(',').Select(s => s.Trim());
            int mask = 0;
            foreach (var name in names)
            {
                int layer = LayerMask.NameToLayer(name);
                if (layer >= 0)
                {
                    mask |= (1 << layer);
                }
            }
            return mask > 0 ? mask : Physics.DefaultRaycastLayers;
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
