using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.Models;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Updates a file based on the user's prompt.")]
public class UpdateFileJarvisModule : BaseJarvisModule
{
    [TacticalComponent("The user's prompt describing the updates to the file.", "string", true)]
    public string Prompt { get; set; }

    [TacticalComponent("The model to use for updating the file content. Defaults to 'BaseModel' if not explicitly specified.", "string")]
    public string Model { get; set; } = ModelName.BaseModel.ToString();

    private readonly IJarvisConfigManager _jarvisConfigManager;
    private readonly IMemoryManager _memoryManager;
    private readonly ILlmClient _llmClient;

    public UpdateFileJarvisModule(IJarvisConfigManager jarvisConfigManager, IMemoryManager memoryManager,
        ILlmClient llmClient)
    {
        _jarvisConfigManager = jarvisConfigManager;
        _memoryManager = memoryManager;
        _llmClient = llmClient;
    }

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            string scratchPadDir = _jarvisConfigManager.GetValue("SCRATCH_PAD_DIR") ?? "./scratchpad";
            Directory.CreateDirectory(scratchPadDir);

            var availableFiles = Directory.GetFiles(scratchPadDir);
            string availableFilesStr = string.Join(", ", availableFiles);

            string selectFilePrompt = $@"
<purpose>
    Select a file from the available files based on the user's prompt.
</purpose>

<instructions>
    <instruction>Based on the user's prompt and the list of available files, infer which file the user wants to update.</instruction>
    <instruction>If no file matches, return an empty string for 'file'.</instruction>
</instructions>

<available-files>
    {availableFilesStr}
</available-files>

<user-prompt>
    {Prompt}
</user-prompt>
";

            cancellationToken.ThrowIfCancellationRequested();

            FileSelectionResponse fileSelectionResponse =
                await _llmClient.StructuredOutputPrompt<FileSelectionResponse>(selectFilePrompt,
                    Constants.ModelNameToId[ModelName.FastModel]);

            if (string.IsNullOrEmpty(fileSelectionResponse.File))
            {
                return new Dictionary<string, object>
                {
                    { "status", "No matching file found" }
                };
            }

            string selectedFile = fileSelectionResponse.File;
            string filePath = Path.Combine(scratchPadDir, selectedFile);

            if (!File.Exists(filePath))
            {
                return new Dictionary<string, object>
                {
                    { "status : File does not exist", $"file_name : {selectedFile}" }
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            string fileContent = await File.ReadAllTextAsync(filePath);
            string memoryContent = _memoryManager.GetXmlForPrompt(new List<string> { "*" });

            string updateFilePrompt = $@"
<purpose>
    Update the content of the file based on the user's prompt, the current file content, and the current memory content.
</purpose>

<instructions>
    <instruction>Based on the user's prompt, the current file content, and the current memory content, generate the updated content for the file.</instruction>
    <instruction>The file-name is the name of the file to update.</instruction>
    <instruction>The user's prompt describes the updates to make.</instruction>
    <instruction>Consider the current memory content when generating the file updates, if relevant.</instruction>
    <instruction>Respond exclusively with the updates to the file and nothing else; they will be used to overwrite the file entirely using f.write().</instruction>
    <instruction>Do not include any preamble or commentary or markdown formatting, just the raw updates.</instruction>
    <instruction>Be precise and accurate.</instruction>
    <instruction>If code generation was requested, be sure to output runnable code, don't include any markdown formatting.</instruction>
</instructions>

<file-name>
    {selectedFile}
</file-name>

<file-content>
    {fileContent}
</file-content>

{memoryContent}

<user-prompt>
    {Prompt}
</user-prompt>
";

            cancellationToken.ThrowIfCancellationRequested();

            string modelId = Model != null
                ? Constants.ModelNameToId[Enum.Parse<ModelName>(Model)]
                : Constants.ModelNameToId[ModelName.BaseModel];
            string fileUpdateResponse = await _llmClient.ChatPrompt(updateFilePrompt, modelId);

            cancellationToken.ThrowIfCancellationRequested();
            await File.WriteAllTextAsync(filePath, fileUpdateResponse);

            return new Dictionary<string, object>
            {
                { "status", "File updated" },
                { "file_name", selectedFile },
                { "model_used", Model ?? ModelName.BaseModel.ToString() },
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
                { "message", $"Failed to update file: {e.Message}" },
            };
        }
    }
}
