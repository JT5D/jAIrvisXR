using System;
using System.Collections.Generic;
using UnityEngine;

namespace jAIrvisXR.AI.Agent
{
    /// <summary>
    /// Dispatches SemanticActions from the daemon into Unity scene operations.
    /// Attach to a GameObject alongside DaemonAgentService, or wire via events.
    /// </summary>
    public class SceneCommandExecutor : MonoBehaviour
    {
        [Header("Scene Settings")]
        [SerializeField] private Transform _spawnParent;
        [SerializeField] private float _spawnDistance = 2f;
        [SerializeField] private float _spawnSpacing = 0.5f;

        [Header("Optional")]
        [SerializeField] private DaemonAgentService _daemonService;

        private readonly Dictionary<string, List<GameObject>> _spawnedObjects = new();
        private readonly List<GameObject> _allSpawned = new();
        private Camera _mainCamera;

        /// <summary>Fired after each action executes. Passes the action and spawned object (if any).</summary>
        public event Action<SemanticAction, GameObject> OnActionExecuted;

        private void Awake()
        {
            _mainCamera = Camera.main;
            if (_spawnParent == null)
                _spawnParent = transform;
        }

        private void OnEnable()
        {
            if (_daemonService == null)
                _daemonService = GetComponent<DaemonAgentService>();

            if (_daemonService != null)
                _daemonService.OnSceneActions += ExecuteActions;
        }

        private void OnDisable()
        {
            if (_daemonService != null)
                _daemonService.OnSceneActions -= ExecuteActions;
        }

        /// <summary>
        /// Execute an array of semantic actions sequentially.
        /// </summary>
        public void ExecuteActions(SemanticAction[] actions)
        {
            foreach (var action in actions)
            {
                try
                {
                    ExecuteAction(action);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SceneCommandExecutor] Failed to execute {action.Type}: {ex.Message}");
                }
            }
        }

        public void ExecuteAction(SemanticAction action)
        {
            switch (action.Type)
            {
                case SemanticActionType.ADD_OBJECT:
                    HandleAddObject(action);
                    break;
                case SemanticActionType.REMOVE_OBJECT:
                    HandleRemoveObject(action);
                    break;
                case SemanticActionType.MODIFY_OBJECTS:
                    HandleModifyObjects(action);
                    break;
                case SemanticActionType.TRANSFORM_OBJECT:
                    HandleTransformObject(action);
                    break;
                case SemanticActionType.CLEAR_SCENE:
                    HandleClearScene(action);
                    break;
                case SemanticActionType.ADD_EMITTER:
                    HandleAddEmitter(action);
                    break;
                case SemanticActionType.CHANGE_LIGHTING:
                    HandleChangeLighting(action);
                    break;
                default:
                    Debug.LogWarning($"[SceneCommandExecutor] Unhandled action type: {action.Type}");
                    break;
            }
        }

        private void HandleAddObject(SemanticAction action)
        {
            string shape = (action.Params.Shape ?? "cube").ToLower();
            int count = Mathf.Max(1, action.Params.Count);

            PrimitiveType primitiveType = shape switch
            {
                "sphere" => PrimitiveType.Sphere,
                "cylinder" => PrimitiveType.Cylinder,
                "capsule" => PrimitiveType.Capsule,
                "plane" => PrimitiveType.Plane,
                "quad" => PrimitiveType.Quad,
                _ => PrimitiveType.Cube
            };

            Vector3 basePos = GetSpawnPosition();

            for (int i = 0; i < count; i++)
            {
                GameObject obj = GameObject.CreatePrimitive(primitiveType);
                obj.name = $"Jarvis_{shape}_{_allSpawned.Count}";
                obj.transform.SetParent(_spawnParent);

                // Position: spread objects along X axis if multiple
                Vector3 offset = count > 1
                    ? new Vector3((i - (count - 1) * 0.5f) * _spawnSpacing, 0, 0)
                    : Vector3.zero;
                obj.transform.position = basePos + offset;

                // Apply position override from params
                if (action.Params.Position is { Length: >= 3 })
                {
                    obj.transform.position = new Vector3(
                        action.Params.Position[0],
                        action.Params.Position[1],
                        action.Params.Position[2]);
                }

                // Apply scale from params
                if (action.Params.Scale is { Length: >= 3 })
                {
                    obj.transform.localScale = new Vector3(
                        action.Params.Scale[0],
                        action.Params.Scale[1],
                        action.Params.Scale[2]);
                }

                // Apply color
                if (!string.IsNullOrEmpty(action.Params.Color))
                    ApplyColor(obj, action.Params.Color);

                // Register
                _allSpawned.Add(obj);
                if (!_spawnedObjects.ContainsKey(shape))
                    _spawnedObjects[shape] = new List<GameObject>();
                _spawnedObjects[shape].Add(obj);

                Debug.Log($"[SceneCommandExecutor] Added {obj.name} at {obj.transform.position}");
                OnActionExecuted?.Invoke(action, obj);
            }
        }

        private void HandleRemoveObject(SemanticAction action)
        {
            string shape = (action.Params.Shape ?? "").ToLower();

            if (!string.IsNullOrEmpty(shape) && _spawnedObjects.TryGetValue(shape, out var list))
            {
                // Remove the most recently added of this shape
                if (list.Count > 0)
                {
                    var obj = list[^1];
                    list.RemoveAt(list.Count - 1);
                    _allSpawned.Remove(obj);
                    Destroy(obj);
                    Debug.Log($"[SceneCommandExecutor] Removed last {shape}");
                    OnActionExecuted?.Invoke(action, null);
                    return;
                }
            }

            // Remove by target name
            if (!string.IsNullOrEmpty(action.Params.TargetObject))
            {
                var target = FindSpawnedByName(action.Params.TargetObject);
                if (target != null)
                {
                    _allSpawned.Remove(target);
                    Destroy(target);
                    Debug.Log($"[SceneCommandExecutor] Removed {action.Params.TargetObject}");
                    OnActionExecuted?.Invoke(action, null);
                    return;
                }
            }

            // Fallback: remove last spawned
            if (_allSpawned.Count > 0)
            {
                var last = _allSpawned[^1];
                _allSpawned.RemoveAt(_allSpawned.Count - 1);
                Destroy(last);
                Debug.Log("[SceneCommandExecutor] Removed last spawned object");
            }
            OnActionExecuted?.Invoke(action, null);
        }

        private void HandleModifyObjects(SemanticAction action)
        {
            string shape = (action.Params.Shape ?? "").ToLower();
            var targets = GetTargets(shape, action.Params.TargetObject);

            foreach (var obj in targets)
            {
                if (!string.IsNullOrEmpty(action.Params.Color))
                    ApplyColor(obj, action.Params.Color);

                if (!string.IsNullOrEmpty(action.Params.Property) && !string.IsNullOrEmpty(action.Params.Value))
                    ApplyProperty(obj, action.Params.Property, action.Params.Value);
            }

            Debug.Log($"[SceneCommandExecutor] Modified {targets.Count} object(s)");
            OnActionExecuted?.Invoke(action, null);
        }

        private void HandleTransformObject(SemanticAction action)
        {
            string shape = (action.Params.Shape ?? "").ToLower();
            var targets = GetTargets(shape, action.Params.TargetObject);

            foreach (var obj in targets)
            {
                if (action.Params.Position is { Length: >= 3 })
                    obj.transform.position = new Vector3(
                        action.Params.Position[0], action.Params.Position[1], action.Params.Position[2]);

                if (action.Params.Rotation is { Length: >= 3 })
                    obj.transform.eulerAngles = new Vector3(
                        action.Params.Rotation[0], action.Params.Rotation[1], action.Params.Rotation[2]);

                if (action.Params.Scale is { Length: >= 3 })
                    obj.transform.localScale = new Vector3(
                        action.Params.Scale[0], action.Params.Scale[1], action.Params.Scale[2]);
            }

            Debug.Log($"[SceneCommandExecutor] Transformed {targets.Count} object(s)");
            OnActionExecuted?.Invoke(action, null);
        }

        private void HandleClearScene(SemanticAction action)
        {
            int count = _allSpawned.Count;
            foreach (var obj in _allSpawned)
            {
                if (obj != null) Destroy(obj);
            }
            _allSpawned.Clear();
            _spawnedObjects.Clear();

            Debug.Log($"[SceneCommandExecutor] Cleared {count} object(s)");
            OnActionExecuted?.Invoke(action, null);
        }

        private void HandleAddEmitter(SemanticAction action)
        {
            // Create a particle system at spawn position
            var go = new GameObject($"Jarvis_emitter_{action.Params.Name ?? "default"}");
            go.transform.SetParent(_spawnParent);
            go.transform.position = GetSpawnPosition();

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = 100;
            main.startLifetime = 2f;

            if (!string.IsNullOrEmpty(action.Params.Color) && TryParseColor(action.Params.Color, out var color))
                main.startColor = color;

            _allSpawned.Add(go);
            Debug.Log($"[SceneCommandExecutor] Added particle emitter: {action.Params.Name}");
            OnActionExecuted?.Invoke(action, go);
        }

        private void HandleChangeLighting(SemanticAction action)
        {
            var sun = FindAnyObjectByType<Light>();
            if (sun == null) return;

            if (!string.IsNullOrEmpty(action.Params.Color) && TryParseColor(action.Params.Color, out var color))
                sun.color = color;

            Debug.Log("[SceneCommandExecutor] Changed scene lighting");
            OnActionExecuted?.Invoke(action, sun.gameObject);
        }

        // --- Helpers ---

        private Vector3 GetSpawnPosition()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_mainCamera != null)
            {
                return _mainCamera.transform.position +
                       _mainCamera.transform.forward * _spawnDistance;
            }

            return _spawnParent.position + Vector3.forward * _spawnDistance;
        }

        private List<GameObject> GetTargets(string shape, string targetName)
        {
            var targets = new List<GameObject>();

            if (!string.IsNullOrEmpty(targetName))
            {
                var obj = FindSpawnedByName(targetName);
                if (obj != null) targets.Add(obj);
            }
            else if (!string.IsNullOrEmpty(shape) && _spawnedObjects.TryGetValue(shape, out var list))
            {
                targets.AddRange(list);
            }
            else
            {
                targets.AddRange(_allSpawned);
            }

            return targets;
        }

        private GameObject FindSpawnedByName(string name)
        {
            foreach (var obj in _allSpawned)
            {
                if (obj != null && obj.name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return obj;
            }
            return null;
        }

        private static void ApplyColor(GameObject obj, string colorStr)
        {
            if (!TryParseColor(colorStr, out var color)) return;

            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;

            // Use a new material instance to avoid shared-material side effects
            renderer.material.color = color;
        }

        private static bool TryParseColor(string colorStr, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrEmpty(colorStr)) return false;

            // Try hex: #FF0000
            if (colorStr.StartsWith("#") && ColorUtility.TryParseHtmlString(colorStr, out color))
                return true;

            // Try named colors
            color = colorStr.ToLower() switch
            {
                "red" => Color.red,
                "green" => Color.green,
                "blue" => Color.blue,
                "yellow" => Color.yellow,
                "white" => Color.white,
                "black" => Color.black,
                "gray" or "grey" => Color.gray,
                "cyan" => Color.cyan,
                "magenta" or "pink" => Color.magenta,
                "orange" => new Color(1f, 0.65f, 0f),
                "purple" => new Color(0.5f, 0f, 0.5f),
                "brown" => new Color(0.6f, 0.3f, 0f),
                "gold" => new Color(1f, 0.84f, 0f),
                "silver" => new Color(0.75f, 0.75f, 0.75f),
                _ => Color.white
            };

            return colorStr.ToLower() != "white" || colorStr.Equals("white", StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyProperty(GameObject obj, string property, string value)
        {
            switch (property.ToLower())
            {
                case "scale":
                    if (float.TryParse(value, out float s))
                        obj.transform.localScale = Vector3.one * s;
                    break;
                case "visible":
                    var r = obj.GetComponent<Renderer>();
                    if (r != null) r.enabled = value.ToLower() == "true";
                    break;
            }
        }
    }
}
