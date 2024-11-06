using Jarvis.Ai.LLM;

namespace Jarvis.Ai.Persistence
{
    public interface IConversationStore
    {
        Task SaveMessageAsync(Message message);
        Task<List<Message>> GetAllMessagesAsync();
    }

    public class ConversationStore : IConversationStore
    {
        private readonly List<MessageWithTimestamp> _messages;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public ConversationStore()
        {
            _messages = new List<MessageWithTimestamp>();
        }

        public async Task SaveMessageAsync(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            await _lock.WaitAsync();
            try
            {
                if (message.Role == "system")
                {
                    var existingSystemMessage = _messages.FirstOrDefault(m => m.Message.Role == "system");
                    if (existingSystemMessage != null)
                    {
                        return;
                    }
                }

                _messages.Add(new MessageWithTimestamp 
                { 
                    Message = message, 
                    Timestamp = DateTime.UtcNow 
                });
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<List<Message>> GetAllMessagesAsync()
        {
            await _lock.WaitAsync();
            try
            {
                return _messages.Select(m => m.Message).ToList();
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    public class MessageWithTimestamp
    {
        public Message Message { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
}