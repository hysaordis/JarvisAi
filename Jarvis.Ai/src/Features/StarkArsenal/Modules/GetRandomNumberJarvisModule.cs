using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Returns a random number between 1 and 100.")]
public class GetRandomNumberJarvisModule : BaseJarvisModule
{
    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            Random rnd = new Random();
            int randomNumber = rnd.Next(1, 101);
            return new Dictionary<string, object>
            {
                { "random_number", randomNumber },
            };
        }
        catch (OperationCanceledException)
        {
            return new Dictionary<string, object>
            {
                { "status", "cancelled" },
                { "message", "Operation was cancelled" }
            };
        }
        catch (Exception e)
        {
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "message", $"Failed to generate random number: {e.Message}" }
            };
        }
    }
}
