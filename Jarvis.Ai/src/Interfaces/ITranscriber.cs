namespace Jarvis.Ai.Interfaces
{
    public interface ITranscriber
    {
        /// <summary>
        /// Initializes the transcriber asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the initialization process.</param>
        /// <returns>A task representing the initialization operation.</returns>
        Task InitializeAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Starts the audio capture and transcription process.
        /// </summary>
        void StartListening();

        /// <summary>
        /// Stops the audio capture and transcription process.
        /// </summary>
        void StopListening();

        /// <summary>
        /// Event that is raised when a final transcription result is available.
        /// </summary>
        event EventHandler<string> OnTranscriptionResult;

        /// <summary>
        /// Event that is raised when a partial transcription result is available.
        /// </summary>
        event EventHandler<string> PartialTranscriptReceived;
    }
}
