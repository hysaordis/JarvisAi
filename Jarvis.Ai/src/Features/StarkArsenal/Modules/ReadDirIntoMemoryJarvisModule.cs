using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Reads all files from the ISOLATION_AREA and saves their content into memory.")]
public class ReadDirIntoMemoryJarvisModule : BaseJarvisModule
{
    private readonly IJarvisConfigManager _jarvisConfigManager;
    private readonly IMemoryManager _memoryManager;

    public ReadDirIntoMemoryJarvisModule(IJarvisConfigManager jarvisConfigManager, IMemoryManager memoryManager)
    {
        _jarvisConfigManager = jarvisConfigManager;
        _memoryManager = memoryManager;
    }

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync(CancellationToken cancellationToken)
    {
        string scratchPadDir = _jarvisConfigManager.GetValue("ISOLATION_AREA") ?? "./scratchpad";

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var files = Directory.GetFiles(scratchPadDir);
            foreach (var filePath in files)
            {
                if (File.Exists(filePath))
                {
                    string content = await File.ReadAllTextAsync(filePath);
                    string fileName = Path.GetFileName(filePath);
                    _memoryManager.Upsert(fileName, content);
                }
            }

            return new Dictionary<string, object>
            {
                { "status", "success" },
                { "message", $"All files from '{scratchPadDir}' have been read into memory" },
                { "files_read", files.Length }
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
                { "message", $"Failed to read directory into memory: {e.Message}" }
            };
        }
    }
}