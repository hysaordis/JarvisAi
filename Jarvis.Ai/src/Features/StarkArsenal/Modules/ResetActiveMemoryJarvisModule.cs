using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Resets the active memory to an empty dictionary.")]
public class ResetActiveMemoryJarvisModule : BaseJarvisModule
{
    [TacticalComponent("Whether to force reset the memory without confirmation. Defaults to false if not specified.", "boolean")]
    public bool ForceDelete { get; set; } = false;

    private readonly IMemoryManager _memoryManager;

    public ResetActiveMemoryJarvisModule(IMemoryManager memoryManager)
    {
        _memoryManager = memoryManager;
    }

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ForceDelete)
            {
                return new Dictionary<string, object>
                {
                    { "status", "confirmation_required" },
                    {
                        "message",
                        "Are you sure you want to reset the active memory? This action cannot be undone. Reply with 'force delete' to confirm."
                    },
                };
            }

            cancellationToken.ThrowIfCancellationRequested();
            _memoryManager.Reset();

            return new Dictionary<string, object>
            {
                { "status", "success" },
                { "message", "Active memory has been reset to an empty dictionary." },
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
                { "message", $"Failed to reset active memory: {e.Message}" }
            };
        }
    }
}
