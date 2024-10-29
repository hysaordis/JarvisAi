using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Adds a key-value pair to memory.")]
public class AddToMemoryJarvisModule : BaseJarvisModule
{
    [TacticalComponent("The key to use for storing the value in memory.", "string", true)]
    public string Key { get; set; }

    [TacticalComponent("The value to store in memory.", "string", true)]
    public string Value { get; set; }

    private readonly IMemoryManager _memoryManager;

    public AddToMemoryJarvisModule(IMemoryManager memoryManager)
    {
        _memoryManager = memoryManager;
    }

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();


            bool success = _memoryManager.Upsert(Key, Value);
            if (success)
            {
                return new Dictionary<string, object>
                {
                    { "status", "success" },
                    { "message", $"Added '{Key}' to memory with value '{Value}'" },
                };
            }
            else
            {
                return new Dictionary<string, object>
                {
                    { "status", "error" },
                    { "message", $"Failed to add '{Key}' to memory" },
                };
            }
        }
        catch (OperationCanceledException)
        {
            return new Dictionary<string, object>
            {
                { "status", "cancelled" },
                { "message", "Operation was cancelled" }
            };
        }
        catch (Exception)
        {
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "message", $"Failed to add '{Key}' to memory" },
            };
        }
    }
}