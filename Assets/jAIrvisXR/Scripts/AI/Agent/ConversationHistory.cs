using System.Collections.Generic;

namespace jAIrvisXR.AI.Agent
{
    public class ConversationHistory
    {
        private readonly List<ClaudeMessage> _messages = new();
        private readonly int _maxTurns;

        public ConversationHistory(int maxTurns)
        {
            _maxTurns = maxTurns;
        }

        public IReadOnlyList<ClaudeMessage> Messages => _messages;

        public void AddUserMessage(string content)
        {
            _messages.Add(new ClaudeMessage { role = "user", content = content });
            TrimIfNeeded();
        }

        public void AddAssistantMessage(string content)
        {
            _messages.Add(new ClaudeMessage { role = "assistant", content = content });
            TrimIfNeeded();
        }

        public void Clear() => _messages.Clear();

        public List<ClaudeMessage> GetMessagesForRequest()
        {
            return new List<ClaudeMessage>(_messages);
        }

        private void TrimIfNeeded()
        {
            int maxMessages = _maxTurns * 2;
            while (_messages.Count > maxMessages)
            {
                _messages.RemoveAt(0);
            }
        }
    }
}
