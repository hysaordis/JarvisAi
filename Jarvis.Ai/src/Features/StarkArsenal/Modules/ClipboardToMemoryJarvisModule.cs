using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;
using TextCopy;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Copies the content from the clipboard to memory.")]
public class ClipboardToMemoryJarvisModule : BaseJarvisModule
{
    [TacticalComponent("The key to use for storing the clipboard content in memory. If not provided, a default key 'clipboard_content' will be used.", "string")]
    public string Key { get; set; }

    private readonly IMemoryManager _memoryManager;

    public ClipboardToMemoryJarvisModule(IMemoryManager memoryManager)
    {
        _memoryManager = memoryManager;
    }

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync()
    {
        try
        {
            string clipboardContent = await ClipboardService.GetTextAsync();
            string memoryKey = Key ?? "clipboard_content";
            _memoryManager.Upsert(memoryKey, clipboardContent);
            return new Dictionary<string, object>
            {
                { "status", "success" },
                { "key", memoryKey },
                { "message", $"Clipboard content stored in memory under key '{memoryKey}'" },
            };
        }
        catch (Exception e)
        {
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "message", $"Failed to copy clipboard content to memory: {e.Message}" },
            };
        }
    }
}