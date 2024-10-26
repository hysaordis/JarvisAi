using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.Models;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Generates content for a new file based on the user's prompt and file name.")]
public class CreateFileJarvisModule : BaseJarvisModule
{
    [TacticalComponent("The name of the file to create.", "string", true)]
    public string FileName { get; set; }

    [TacticalComponent("The user's prompt to generate the file content.", "string", true)]
    public string Prompt { get; set; }

    private readonly IJarvisConfigManager _jarvisConfigManager;
    private readonly IMemoryManager _memoryManager;
    private readonly ILlmClient _llmClient;

    public CreateFileJarvisModule(IJarvisConfigManager jarvisConfigManager, IMemoryManager memoryManager,
        ILlmClient llmClient)
    {
        _jarvisConfigManager = jarvisConfigManager;
        _memoryManager = memoryManager;
        _llmClient = llmClient;
    }

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync()
    {
        string? scratchPadDir = _jarvisConfigManager.GetValue("SCRATCH_PAD_DIR");
        Directory.CreateDirectory(scratchPadDir);

        string filePath = Path.Combine(scratchPadDir, FileName);

        if (File.Exists(filePath))
        {
            return new Dictionary<string, object>
            {
                { "status", "file already exists" }
            };
        }

        string memoryContent = _memoryManager.GetXmlForPrompt(new List<string> { "*" });

        string promptStructure = $@"
<purpose>
    Generate content for a new file based on the user's prompt, the file name, and the current memory content.
</purpose>

<instructions>
    <instruction>Based on the user's prompt, the file name, and the current memory content, generate content for a new file.</instruction>
    <instruction>The file name is the name of the file that the user wants to create.</instruction>
    <instruction>The user's prompt is the prompt that the user wants to use to generate the content for the new file.</instruction>
    <instruction>Consider the current memory content when generating the file content, if relevant.</instruction>
    <instruction>If code generation was requested, be sure to output runnable code, don't include any markdown formatting.</instruction>
</instructions>

<user-prompt>
    {Prompt}
</user-prompt>

<file-name>
    {FileName}
</file-name>

{memoryContent}
";

        CreateFileResponse response =
            await _llmClient.StructuredOutputPrompt<CreateFileResponse>(promptStructure,
                Constants.ModelNameToId[ModelName.BaseModel]);

        await File.WriteAllTextAsync(filePath, response.FileContent);

        return new Dictionary<string, object>
        {
            { "status", "file created" },
            { "file_name", response.FileName }
        };
    }
}