public class AlitaHub : Hub
{
    // ...existing code...

    public Task<string> GetServiceStatus()
    {
        try {
            // Implementa la logica per verificare lo stato del servizio
            return Task.FromResult(_audioService.GetStatus());
        }
        catch (Exception ex) {
            return Task.FromResult("Disconnected");
        }
    }

    public async Task InitializeServiceAsync()
    {
        try {
            // Implementa la logica di inizializzazione
            await _audioService.Initialize();
        }
        catch (Exception ex) {
            throw new HubException($"Failed to initialize service: {ex.Message}");
        }
    }
}
