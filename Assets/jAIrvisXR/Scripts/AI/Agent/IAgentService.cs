using System.Threading;
using System.Threading.Tasks;

namespace jAIrvisXR.AI.Agent
{
    public struct AgentRequest
    {
        public string UserMessage;
        public string ConversationId;
    }

    public struct AgentResponse
    {
        public string Text;
        public bool IsSuccess;
        public string ErrorMessage;
        public string StopReason;

        public static AgentResponse Success(string text, string stopReason = "end_turn")
            => new() { Text = text, IsSuccess = true, StopReason = stopReason };

        public static AgentResponse Failure(string error)
            => new() { IsSuccess = false, ErrorMessage = error };
    }

    public interface IAgentService
    {
        string ServiceName { get; }
        bool IsReady { get; }

        Task InitializeAsync(CancellationToken cancellationToken = default);
        Task<AgentResponse> SendMessageAsync(AgentRequest request,
            CancellationToken cancellationToken = default);
        void ResetConversation();
        void Dispose();
    }
}
