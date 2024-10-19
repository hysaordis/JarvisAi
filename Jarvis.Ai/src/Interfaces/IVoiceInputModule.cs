namespace Jarvis.Ai.Interfaces
{
    public interface IVoiceInputModule
    {
        void StartListening();
        void StopListening();
        byte[]? GetAudioData();
        void Close();
    }
}
