using System;
using System.Collections.Generic;

namespace jAIrvisXR.AI.Agent
{
    [Serializable]
    public class ClaudeMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    public class ClaudeRequestBody
    {
        public string model;
        public int max_tokens;
        public float temperature;
        public string system;
        public List<ClaudeMessage> messages;
    }

    [Serializable]
    public class ClaudeContentBlock
    {
        public string type;
        public string text;
    }

    [Serializable]
    public class ClaudeUsage
    {
        public int input_tokens;
        public int output_tokens;
    }

    [Serializable]
    public class ClaudeResponseWrapper
    {
        public string id;
        public string type;
        public string role;
        public string model;
        public string stop_reason;
        public ClaudeContentBlock[] content;
        public ClaudeUsage usage;
    }
}
