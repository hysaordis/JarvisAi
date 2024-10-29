using Newtonsoft.Json;
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
        private readonly string _storageFile;
        private readonly string _storageDirectory;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public ConversationStore(string storagePath = "conversations.json")
        {
            _storageFile = storagePath;
            _storageDirectory = Path.GetDirectoryName(_storageFile) ?? "";

            InitializeStorage();
        }

        private void InitializeStorage()
        {
            try
            {
                if (!string.IsNullOrEmpty(_storageDirectory) && !Directory.Exists(_storageDirectory))
                {
                    Directory.CreateDirectory(_storageDirectory);
                }

                if (!File.Exists(_storageFile))
                {
                    var emptyList = new List<MessageWithTimestamp>();
                    var json = JsonConvert.SerializeObject(emptyList, Formatting.Indented);
                    File.WriteAllText(_storageFile, json);
                }
                else
                {
                    try
                    {
                        var json = File.ReadAllText(_storageFile);
                        JsonConvert.DeserializeObject<List<MessageWithTimestamp>>(json);
                    }
                    catch (JsonException)
                    {
                        var emptyList = new List<MessageWithTimestamp>();
                        var newJson = JsonConvert.SerializeObject(emptyList, Formatting.Indented);
                        File.WriteAllText(_storageFile, newJson);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize conversation storage at {_storageFile}", ex);
            }
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
                var messages = await LoadMessagesAsync();

                if (message.Role == "system")
                {
                    var existingSystemMessage = messages.FirstOrDefault(m => m.Message.Role == "system");
                    if (existingSystemMessage != null)
                    {
                        return;
                    }
                }

                messages.Add(new MessageWithTimestamp { Message = message, Timestamp = DateTime.UtcNow });
                var json = JsonConvert.SerializeObject(messages, Formatting.Indented);
                await File.WriteAllTextAsync(_storageFile, json);
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
                var messagesWithTimestamps = await LoadMessagesAsync();
                return messagesWithTimestamps.Select(m => m.Message).ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<List<MessageWithTimestamp>> LoadMessagesAsync()
        {
            var json = await File.ReadAllTextAsync(_storageFile);
            return JsonConvert.DeserializeObject<List<MessageWithTimestamp>>(json)
                   ?? new List<MessageWithTimestamp>();
        }
    }

    public class MessageWithTimestamp
    {
        public Message Message { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
}
