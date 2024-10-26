using System.Net.WebSockets;
using System.Text;
using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Interfaces;
using Newtonsoft.Json;

namespace Jarvis.Ai
{
    public class JarvisAgent : IJarvis
    {
        private ClientWebSocket _networkInterface;
        private dynamic _pendingTask;
        private readonly string _serverEndpoint;
        private readonly Dictionary<string, string> _headers;
        private readonly StarkProtocols _starkProtocols;
        private readonly IModuleRegistry _moduleRegistry;
        private readonly IStarkArsenal _starkArsenal;
        private readonly IJarvisLogger _jarvisLogger;
        private readonly IAudioOutputModule _audioOutputModule;

        public JarvisAgent(IJarvisConfigManager configManager, StarkProtocols starkProtocols, IModuleRegistry moduleRegistry,
            IStarkArsenal starkArsenal, IJarvisLogger jarvisLogger, IAudioOutputModule audioOutputModule)
        {
            var accessKey = configManager.GetValue("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(accessKey))
            {
                throw new Exception("OPENAI_API_KEY environment variable not set.");
            }
            _starkProtocols = starkProtocols;
            _moduleRegistry = moduleRegistry;
            _starkArsenal = starkArsenal;
            _jarvisLogger = jarvisLogger;
            _audioOutputModule = audioOutputModule;
            _serverEndpoint = "wss://api.openai.com/v1/realtime?model=gpt-4o-realtime-preview-2024-10-01";
            _headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {accessKey}" },
                { "OpenAI-Beta", "realtime=v1" }
            };
        }

        public async Task InitializeAsync(string[]? initialCommands, CancellationToken cancellationToken)
        {
            await ConnectToServerAsync(cancellationToken);
            await ConfigureSessionAsync(cancellationToken);

            if (initialCommands is { Length: > 0 })
            {
                await ExecuteCommandsAsync(initialCommands, cancellationToken);
            }
        }

        private async Task ConnectToServerAsync(CancellationToken cancellationToken)
        {
            _networkInterface = new ClientWebSocket();
            foreach (var header in _headers)
            {
                _networkInterface.Options.SetRequestHeader(header.Key, header.Value);
            }

            await _networkInterface.ConnectAsync(new Uri(_serverEndpoint), cancellationToken);
        }

        private async Task ConfigureSessionAsync(CancellationToken cancellationToken)
        {
            var sessionConfig = new
            {
                type = "session.update",
                session = new
                {
                    modalities = new[] { "text", "audio" },
                    instructions = _starkProtocols.SessionInstructions,
                    voice = "alloy",
                    input_audio_format = "pcm16",
                    output_audio_format = "pcm16",
                    turn_detection = new
                    {
                        type = "server_vad",
                        threshold = StarkProtocols.SilenceThreshold,
                        prefix_padding_ms = StarkProtocols.PrefixPaddingMs,
                        silence_duration_ms = StarkProtocols.SilenceDurationMs,
                    },
                    tools = _starkArsenal.GetTacticalArray(),
                }
            };
            await TransmitAsync(sessionConfig, cancellationToken);
        }

        public async Task ProcessAudioInputAsync(byte[] audioData, CancellationToken cancellationToken)
        {
            var base64Audio = Convert.ToBase64String(audioData);
            var audioMessage = new
            {
                type = "input_audio_buffer.append",
                audio = base64Audio,
            };
            await TransmitAsync(audioMessage, cancellationToken);
            await TransmitAsync(new { type = "input_audio_buffer.commit" }, cancellationToken);
        }

        public async Task<string> ListenForResponseAsync(CancellationToken cancellationToken)
        {
            var responseText = new StringBuilder();
            var audioDataList = new List<byte>();
            var taskArguments = new StringBuilder();

            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await ReceiveAsync(cancellationToken);
                if (message == null) break;

                var eventObject = JsonConvert.DeserializeObject<dynamic>(message);
                string eventType = eventObject!.type;

                switch (eventType)
                {
                    case "response.output_item.added":
                        if (eventObject.item.type == "function_call")
                        {
                            _pendingTask = eventObject.item;
                            taskArguments.Clear();
                        }

                        break;

                    case "response.function_call_arguments.delta":
                        string delta = eventObject.delta ?? "";
                        taskArguments.Append(delta);
                        break;

                    case "response.function_call_arguments.done":
                        await HandleTaskAsync(taskArguments.ToString(), cancellationToken);
                        taskArguments.Clear();
                        _pendingTask = null!;
                        break;

                    case "response.text.delta":
                        string textDelta = eventObject.delta ?? "";
                        responseText.Append(textDelta);
                        break;

                    case "response.audio.delta":
                        string audioDelta = eventObject.delta ?? "";
                        var audioBytes = Convert.FromBase64String(audioDelta);
                        audioDataList.AddRange(audioBytes);
                        break;

                    case "response.done":
                        if (audioDataList.Count <= 0) return responseText.ToString();
                        await _audioOutputModule.PlayAudioAsync(audioDataList.ToArray(), cancellationToken);
                        audioDataList.Clear();
                        return responseText.ToString();
                }
            }

            return responseText.ToString();
        }

        private async Task HandleTaskAsync(string arguments, CancellationToken cancellationToken)
        {
            if (_pendingTask != null)
            {
                string taskName = _pendingTask.name;
                string callId = _pendingTask.call_id;
                Dictionary<string, object>? args;

                try
                {
                    args = JsonConvert.DeserializeObject<Dictionary<string, object>>(arguments);
                }
                catch
                {
                    args = new Dictionary<string, object>();
                }

                if (args != null)
                {
                    try
                    {
                        var result = await _moduleRegistry.ExecuteCommand(taskName, args);

                        var taskResponse = new
                        {
                            type = "conversation.item.create",
                            item = new
                            {
                                type = "function_call_output",
                                call_id = callId,
                                output = JsonConvert.SerializeObject(result),
                            },
                        };

                        await TransmitAsync(taskResponse, cancellationToken);
                    }
                    catch (KeyNotFoundException)
                    {
                        _jarvisLogger.LogWarning($"Command '{taskName}' not found.");
                    }
                    catch (Exception ex)
                    {
                        _jarvisLogger.LogError($"Error executing command '{taskName}': {ex.Message}");
                    }
                }

                await TransmitAsync(new { type = "response.create" }, cancellationToken);
            }
        }

        public async Task ExecuteCommandsAsync(string[] commands, CancellationToken cancellationToken)
        {
            var content = commands.Select(text => new { type = "input_text", text }).ToArray();
            var commandEvent = new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "message",
                    role = "user",
                    content = content,
                },
            };
            await TransmitAsync(commandEvent, cancellationToken);

            var responseEvent = new { type = "response.create" };
            await TransmitAsync(responseEvent, cancellationToken);
        }

        private async Task TransmitAsync(object message, CancellationToken cancellationToken)
        {
            string json = JsonConvert.SerializeObject(message);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await _networkInterface.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true,
                cancellationToken);
        }

        private async Task<string?> ReceiveAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            using var memoryStream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await _networkInterface.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _networkInterface.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing",
                        cancellationToken);
                    return null;
                }

                memoryStream.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            return Encoding.UTF8.GetString(memoryStream.ToArray());
        }

        public async Task ShutdownAsync()
        {
            if (_networkInterface is { State: WebSocketState.Open })
            {
                await _networkInterface.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing",
                    CancellationToken.None);
            }
        }
    }
}