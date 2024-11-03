public class AudioService : IAudioService
{
    // ...existing code...

    public string GetStatus()
    {
        // Implementa la logica per ottenere lo stato del servizio
        return _assemblyAiClient?.IsConnected == true ? "Connected" : "Disconnected";
    }

    public async Task Initialize()
    {
        // Implementa la logica di inizializzazione
        if (_assemblyAiClient?.IsConnected != true)
        {
            await _assemblyAiClient.Connect();
            // Aggiungi qui altra logica di inizializzazione necessaria
        }
    }
}
