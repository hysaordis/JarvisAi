using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.Models;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Discusses a file's content based on the user's prompt, considering the current memory content.")]
public class DiscussFileJarvisModule : BaseJarvisModule
{
    [TacticalComponent("The user's prompt, question, or statement describing what to discuss about the file content.", "string", true)]
    public string Prompt { get; set; }

    [TacticalComponent("The model to use for discussing the file content. Defaults to 'BaseModel' if not explicitly specified.", "string")]
    public string Model { get; set; } = ModelName.BaseModel.ToString();

    private readonly IJarvisConfigManager _jarvisConfigManager;
    private readonly IMemoryManager _memoryManager;
    private readonly ILlmClient _llmClient;
    private readonly StarkProtocols _starkProtocols;

    public DiscussFileJarvisModule(IJarvisConfigManager jarvisConfigManager, IMemoryManager memoryManager,
        ILlmClient llmClient, StarkProtocols starkProtocols)
    {
        _jarvisConfigManager = jarvisConfigManager;
        _memoryManager = memoryManager;
        _llmClient = llmClient;
        _starkProtocols = starkProtocols;
    }

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? scratchPadDir = _jarvisConfigManager.GetValue("SCRATCH_PAD_DIR");
            string focusFile = _starkProtocols.GetFocusFile();
            string? filePath;

            if (!string.IsNullOrEmpty(focusFile))
            {
                filePath = Path.Combine(scratchPadDir, focusFile);
                if (!File.Exists(filePath))
                {
                    return new Dictionary<string, object>
                    {
                        { "status", "Focus file not found" },
                        { "file_name", focusFile }
                    };
                }
            }
            else
            {
                var availableFiles = Directory.GetFiles(scratchPadDir);
                string availableFilesStr = string.Join(", ", availableFiles);

                string selectFilePrompt = $@"
<purpose>
    Select a file from the available files based on the user's prompt.
</purpose>

<instructions>
    <instruction>Based on the user's prompt and the list of available files, infer which file the user wants to discuss.</instruction>
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
                        { "status", "No matching file found" }
                    };
                }

                filePath = Path.Combine(scratchPadDir, fileSelectionResponse.File);
            }

            cancellationToken.ThrowIfCancellationRequested();

            string fileContent = await File.ReadAllTextAsync(filePath);
            string memoryContent = _memoryManager.GetXmlForPrompt(new List<string> { "*" });

            string discussFilePrompt = $@"
<purpose>
    Discuss the content of the file based on the user's prompt and the current memory content.
</purpose>

<instructions>
    <instruction>Based on the user's prompt, the file content, and the current memory content, provide a relevant discussion or analysis.</instruction>
    <instruction>Be concise and focus on the aspects mentioned in the user's prompt.</instruction>
    <instruction>Consider the current memory content when discussing the file, if relevant.</instruction>
    <instruction>Keep responses short and concise. Keep response under 3 sentences for concise conversations.</instruction>
</instructions>

<file-content>
{fileContent}
</file-content>

{memoryContent}

<user-prompt>
{Prompt}
</user-prompt>
";

            cancellationToken.ThrowIfCancellationRequested();

            string modelId = Constants.ModelNameToId[Enum.Parse<ModelName>(Model)];
            string discussion = await _llmClient.ChatPrompt(discussFilePrompt, modelId);

            return new Dictionary<string, object>
            {
                { "status", "File discussed" },
                { "file_name", Path.GetFileName(filePath) },
                { "discussion", discussion }
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
                { "message", $"Failed to discuss file: {e.Message}" }
            };
        }
    }
}
