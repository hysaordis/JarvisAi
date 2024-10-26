using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.Models;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Reads a file from the scratch_pad_dir and saves its content into memory based on the user's prompt.")]
public class ReadFileIntoMemoryJarvisModule : BaseJarvisModule
{
    [TacticalComponent("The user's prompt describing the file to read into memory.", "string", true)]
    public string Prompt { get; set; }

    private readonly IJarvisConfigManager _jarvisConfigManager;
    private readonly IMemoryManager _memoryManager;
    private readonly ILlmClient _llmClient;

    public ReadFileIntoMemoryJarvisModule(IJarvisConfigManager jarvisConfigManager, IMemoryManager memoryManager,
        ILlmClient llmClient)
    {
        _jarvisConfigManager = jarvisConfigManager;
        _memoryManager = memoryManager;
        _llmClient = llmClient;
    }

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync()
    {
        string? scratchPadDir = _jarvisConfigManager.GetValue("SCRATCH_PAD_DIR");
        var availableFiles = Directory.GetFiles(scratchPadDir);
        string availableFilesStr = string.Join(", ", availableFiles);

        string selectFilePrompt = $@"
<purpose>
    Select a file from the available files based on the user's prompt.
</purpose>

<instructions>
    <instruction>Based on the user's prompt and the list of available files, infer which file the user wants to read into memory.</instruction>
    <instruction>If no file matches, return an empty string for 'file'.</instruction>
</instructions>

<available-files>
    {availableFilesStr}
</available-files>

<user-prompt>
    {Prompt}
</user-prompt>
";

        FileReadResponse fileSelectionResponse =
            await _llmClient.StructuredOutputPrompt<FileReadResponse>(selectFilePrompt,
                Constants.ModelNameToId[ModelName.FastModel]);

        if (string.IsNullOrEmpty(fileSelectionResponse.File))
        {
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "message", "No matching file found" },
            };
        }

        string filePath = Path.Combine(scratchPadDir, fileSelectionResponse.File);

        if (!File.Exists(filePath))
        {
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "message", $"File '{fileSelectionResponse.File}' not found in scratch_pad_dir" },
            };
        }

        try
        {
            string content = await File.ReadAllTextAsync(filePath);
            _memoryManager.Upsert(fileSelectionResponse.File, content);
            return new Dictionary<string, object>
            {
                { "status", "success" },
                { "message", $"File '{fileSelectionResponse.File}' content saved to memory" },
            };
        }
        catch (Exception e)
        {
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "message", $"Failed to read file '{fileSelectionResponse.File}' into memory: {e.Message}" },
            };
        }
    }
}