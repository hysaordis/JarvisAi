using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Returns a random number between 1 and 100.")]
public class GetRandomNumberJarvisModule : BaseJarvisModule
{
    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync()
    {
        Random rnd = new Random();
        int randomNumber = rnd.Next(1, 101);
        return new Dictionary<string, object>
        {
            { "random_number", randomNumber },
        };
    }
}