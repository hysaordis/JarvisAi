using Jarvis.Ai.Interfaces;

namespace Jarvis.Ai
{
    public class IronManSuit
    {
        private readonly JarvisAgent _jarvis;
        private readonly IVoiceInputModule _voiceInput;
        private readonly IDisplayModule _display;

        public IronManSuit(JarvisAgent jarvis, IVoiceInputModule voiceInput, IDisplayModule display)
        {
            _jarvis = jarvis;
            _voiceInput = voiceInput;
            _display = display;
        }

        public async Task ActivateAsync(string[]? startupCommands = null)
        {
            while (true)
            {
                var cancellationTokenSource = new CancellationTokenSource();

                try
                {
                    await _jarvis.InitializeAsync(startupCommands, cancellationTokenSource.Token);

                    _voiceInput.StartListening();

                    var sendAudioTask = Task.Run(async () =>
                    {
                        while (!cancellationTokenSource.IsCancellationRequested)
                        {
                            var audioData = _voiceInput.GetAudioData();
                            if (audioData is { Length: > 0 })
                            {
                                await _jarvis.ProcessAudioInputAsync(audioData, cancellationTokenSource.Token);
                            }

                            await Task.Delay(100, cancellationTokenSource.Token);
                        }
                    }, cancellationTokenSource.Token);

                    var listenTask = Task.Run(async () =>
                    {
                        while (!cancellationTokenSource.IsCancellationRequested)
                        {
                            var response = await _jarvis.ListenForResponseAsync(cancellationTokenSource.Token);
                            if (!string.IsNullOrEmpty(response))
                            {
                                await _display.ShowAsync(response, cancellationTokenSource.Token);
                            }
                        }
                    }, cancellationTokenSource.Token);

                    await Task.WhenAll(sendAudioTask, listenTask);
                    break;
                }
                catch
                {
                    await Task.Delay(1000, cancellationTokenSource.Token);
                }
                finally
                {
                    _voiceInput.StopListening();
                    await _jarvis.ShutdownAsync();
                }
            }
        }
    }
}