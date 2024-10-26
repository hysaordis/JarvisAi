using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.Models;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Deletes a file based on the user's prompt.")]
public class DeleteFileJarvisModule : BaseJarvisModule
{
    [TacticalComponent("The user's prompt describing the file to delete.", "string", true)]
    public string Prompt { get; set; }

    [TacticalComponent("Whether to force delete the file without confirmation. Defaults to 'false' if not specified.", "boolean")]
    public bool ForceDelete { get; set; }

    private readonly IJarvisConfigManager _jarvisConfigManager;
    private readonly ILlmClient _llmClient;

    public DeleteFileJarvisModule(IJarvisConfigManager jarvisConfigManager, ILlmClient llmClient)
    {
        _jarvisConfigManager = jarvisConfigManager;
        _llmClient = llmClient;
    }

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync()
    {
        string scratchPadDir = _jarvisConfigManager.GetValue("SCRATCH_PAD_DIR") ?? "./scratchpad";
        Directory.CreateDirectory(scratchPadDir);

        var availableFiles = Directory.GetFiles(scratchPadDir);
        string availableFilesStr = string.Join(", ", availableFiles);

        string selectFilePrompt = $@"
<purpose>
    Select a file from the available files to delete.
</purpose>

<instructions>
    <instruction>Based on the user's prompt and the list of available files, infer which file the user wants to delete.</instruction>
    <instruction>If no file matches, return an empty string for 'file'.</instruction>
</instructions>

<available-files>
    {availableFilesStr}
</available-files>

<user-prompt>
    {Prompt}
</user-prompt>
";

        FileDeleteResponse fileDeleteResponse =
            await _llmClient.StructuredOutputPrompt<FileDeleteResponse>(selectFilePrompt,
                Constants.ModelNameToId[ModelName.FastModel]);

        if (string.IsNullOrEmpty(fileDeleteResponse.File))
        {
            return new Dictionary<string, object>
            {
                { "status", "No matching file found" }
            };
        }

        string selectedFile = fileDeleteResponse.File;
        string filePath = Path.Combine(scratchPadDir, selectedFile);

        if (!File.Exists(filePath))
        {
            return new Dictionary<string, object>
            {
                { $"status : File does not exist", $"file_name : {selectedFile}" }
            };
        }

        if (!ForceDelete)
        {
            return new Dictionary<string, object>
            {
                { "status", "Confirmation required" },
                { "file_name", selectedFile },
                {
                    "message",
                    $"Are you sure you want to delete '{selectedFile}'? Say force delete if you want to delete."
                }
            };
        }

        File.Delete(filePath);

        return new Dictionary<string, object>
        {
            { "status", "File deleted" },
            { "file_name", selectedFile }
        };
    }
}