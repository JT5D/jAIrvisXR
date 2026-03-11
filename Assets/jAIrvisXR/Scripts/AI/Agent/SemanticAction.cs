using System;
using System.Collections.Generic;
using UnityEngine;

namespace jAIrvisXR.AI.Agent
{
    [Serializable]
    public enum SemanticActionType
    {
        ADD_OBJECT,
        REMOVE_OBJECT,
        MODIFY_OBJECTS,
        TRANSFORM_OBJECT,
        CLEAR_SCENE,
        ADD_EMITTER,
        SET_ANIMATION,
        ADD_COMPONENT,
        SET_PROPERTY,
        ARRANGE_FORMATION,
        SET_VIBE,
        CHANGE_LIGHTING
    }

    [Serializable]
    public struct SemanticAction
    {
        public SemanticActionType Type;
        public SemanticActionParams Params;
        public float Confidence;

        public override string ToString()
            => $"{Type}({Confidence:F2})";
    }

    [Serializable]
    public struct SemanticActionParams
    {
        // ADD_OBJECT / REMOVE_OBJECT
        public string Shape;
        public int Count;
        public string Color;

        // TRANSFORM_OBJECT
        public float[] Position;
        public float[] Rotation;
        public float[] Scale;

        // MODIFY_OBJECTS
        public string Property;
        public string Value;

        // ADD_EMITTER / SET_ANIMATION / SET_VIBE
        public string Name;

        // ADD_COMPONENT / SET_PROPERTY
        public string ComponentType;
        public string TargetObject;
    }

    [Serializable]
    public struct SemanticActionResponse
    {
        public SemanticAction[] Actions;
    }

    [Serializable]
    public struct DaemonTimingMetrics
    {
        public int RouteMs;
        public int LlmMs;
        public int ToolMs;
        public int TotalMs;
    }

    /// <summary>
    /// Raw JSON wrapper matching the daemon /api/command response.
    /// </summary>
    [Serializable]
    internal struct DaemonCommandResponse
    {
        public string action;
        public string response;
        public string provider;
        // timing parsed separately (JsonUtility doesn't handle nested well)
    }

    /// <summary>
    /// Parses daemon JSON into strongly-typed SemanticActions.
    /// Uses Unity's JsonUtility where possible, falls back to manual parsing.
    /// </summary>
    public static class SemanticActionParser
    {
        public static bool TryParseActions(string json, out SemanticAction[] actions)
        {
            actions = null;
            if (string.IsNullOrEmpty(json)) return false;

            try
            {
                // Daemon returns: {"actions":[{...}]}
                // Or: direct array [{...}]
                string actionsJson = json.Trim();

                // Extract the "actions" array if wrapped
                if (actionsJson.StartsWith("{"))
                {
                    int idx = actionsJson.IndexOf("\"actions\"", StringComparison.Ordinal);
                    if (idx < 0) return false;

                    int arrStart = actionsJson.IndexOf('[', idx);
                    int arrEnd = FindMatchingBracket(actionsJson, arrStart);
                    if (arrStart < 0 || arrEnd < 0) return false;

                    actionsJson = actionsJson.Substring(arrStart, arrEnd - arrStart + 1);
                }

                var parsed = ParseActionArray(actionsJson);
                if (parsed == null || parsed.Count == 0) return false;

                actions = parsed.ToArray();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SemanticActionParser] Parse failed: {ex.Message}");
                return false;
            }
        }

        private static List<SemanticAction> ParseActionArray(string arrayJson)
        {
            var result = new List<SemanticAction>();
            int i = 0;
            while (i < arrayJson.Length)
            {
                int objStart = arrayJson.IndexOf('{', i);
                if (objStart < 0) break;

                int objEnd = FindMatchingBrace(arrayJson, objStart);
                if (objEnd < 0) break;

                string objJson = arrayJson.Substring(objStart, objEnd - objStart + 1);
                if (TryParseSingleAction(objJson, out var action))
                    result.Add(action);

                i = objEnd + 1;
            }
            return result;
        }

        private static bool TryParseSingleAction(string json, out SemanticAction action)
        {
            action = default;

            string typeStr = ExtractStringField(json, "type");
            if (string.IsNullOrEmpty(typeStr)) return false;

            if (!Enum.TryParse<SemanticActionType>(typeStr, true, out var actionType))
                return false;

            action.Type = actionType;
            action.Confidence = ExtractFloatField(json, "confidence", 0.5f);

            // Parse params sub-object
            int paramsIdx = json.IndexOf("\"params\"", StringComparison.Ordinal);
            if (paramsIdx >= 0)
            {
                int braceStart = json.IndexOf('{', paramsIdx);
                if (braceStart >= 0)
                {
                    int braceEnd = FindMatchingBrace(json, braceStart);
                    if (braceEnd >= 0)
                    {
                        string paramsJson = json.Substring(braceStart, braceEnd - braceStart + 1);
                        action.Params = ParseParams(paramsJson);
                    }
                }
            }

            return true;
        }

        private static SemanticActionParams ParseParams(string json)
        {
            return new SemanticActionParams
            {
                Shape = ExtractStringField(json, "shape"),
                Count = Mathf.Max(1, ExtractIntField(json, "count", 1)),
                Color = ExtractStringField(json, "color"),
                Position = ExtractFloatArray(json, "position"),
                Rotation = ExtractFloatArray(json, "rotation"),
                Scale = ExtractFloatArray(json, "scale"),
                Property = ExtractStringField(json, "property"),
                Value = ExtractStringField(json, "value"),
                Name = ExtractStringField(json, "name"),
                ComponentType = ExtractStringField(json, "componentType"),
                TargetObject = ExtractStringField(json, "targetObject")
            };
        }

        private static string ExtractStringField(string json, string field)
        {
            string search = $"\"{field}\":\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;
            int start = idx + search.Length;
            int end = json.IndexOf('"', start);
            return end < 0 ? null : json.Substring(start, end - start);
        }

        private static int ExtractIntField(string json, string field, int defaultValue)
        {
            string search = $"\"{field}\":";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return defaultValue;
            int start = idx + search.Length;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
                end++;
            return int.TryParse(json.Substring(start, end - start), out int val) ? val : defaultValue;
        }

        private static float ExtractFloatField(string json, string field, float defaultValue)
        {
            string search = $"\"{field}\":";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return defaultValue;
            int start = idx + search.Length;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.' || json[end] == '-'))
                end++;
            return float.TryParse(json.Substring(start, end - start),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float val) ? val : defaultValue;
        }

        private static float[] ExtractFloatArray(string json, string field)
        {
            string search = $"\"{field}\":[";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;
            int start = idx + search.Length;
            int end = json.IndexOf(']', start);
            if (end < 0) return null;

            string inner = json.Substring(start, end - start);
            string[] parts = inner.Split(',');
            var result = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                float.TryParse(parts[i].Trim(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out result[i]);
            }
            return result;
        }

        private static int FindMatchingBracket(string s, int openIdx)
        {
            if (openIdx < 0 || openIdx >= s.Length) return -1;
            int depth = 0;
            bool inStr = false;
            for (int i = openIdx; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"' && (i == 0 || s[i - 1] != '\\')) inStr = !inStr;
                if (inStr) continue;
                if (c == '[') depth++;
                else if (c == ']') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        private static int FindMatchingBrace(string s, int openIdx)
        {
            if (openIdx < 0 || openIdx >= s.Length) return -1;
            int depth = 0;
            bool inStr = false;
            for (int i = openIdx; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"' && (i == 0 || s[i - 1] != '\\')) inStr = !inStr;
                if (inStr) continue;
                if (c == '{') depth++;
                else if (c == '}') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }
    }
}
