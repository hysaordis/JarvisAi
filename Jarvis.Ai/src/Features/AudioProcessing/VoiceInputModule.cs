using System.Collections.Concurrent;
using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.src.Interfaces;
using NAudio.Wave;

namespace Jarvis.Ai.Features.AudioProcessing
{
    public class VoiceInputModule : IVoiceInputModule
    {
        private readonly WaveInEvent _waveIn;
        private readonly BlockingCollection<byte[]> _audioQueue;
        private readonly IJarvisLogger _jarvisLogger;

        public VoiceInputModule(IJarvisLogger jarvisLogger)
        {
            _jarvisLogger = jarvisLogger;
            _audioQueue = new BlockingCollection<byte[]>();
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(Constants.RATE, Constants.BIT, Constants.CHANNELS),
                BufferMilliseconds = 1016,
                DeviceNumber = 1,
            };
            _waveIn.DataAvailable += OnDataAvailable!;
            _jarvisLogger.LogInformation("AsyncMicrophone initialized");
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            _jarvisLogger.LogDebug($"OnDataAvailable called. Bytes recorded: {e.BytesRecorded}");
            
            byte[] buffer = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, buffer, e.BytesRecorded);
            _audioQueue.Add(buffer);
            _jarvisLogger.LogDebug($"Added {buffer.Length} bytes to audioQueue.");
        }

        public void StartListening()
        {
            _waveIn.StartRecording();
        }

        public void StopListening()
        {
            _waveIn.StopRecording();
        }

        public byte[]? GetAudioData()
        {
            List<byte> data = new List<byte>();
            while (_audioQueue.TryTake(out var buffer))
            {
                data.AddRange(buffer);
            }
            _jarvisLogger.LogDebug($"GetAudioData retrieved {data.Count} bytes.");
            return data.Count > 0 ? data.ToArray() : null;
        }

        public void Close()
        {
            _waveIn.StopRecording();
            _waveIn.Dispose();
            _jarvisLogger.LogInformation("AsyncMicrophone closed");
        }
    }
}
