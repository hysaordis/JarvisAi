using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.Models;
using Newtonsoft.Json;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Removes a variable from memory based on the user's prompt.")]
public class RemoveVariableFromMemoryJarvisModule : BaseJarvisModule
{
    [TacticalComponent("The user's prompt describing which variable to remove from memory.", "string", true)]
    public string Prompt { get; set; }

    private readonly IMemoryManager _memoryManager;
    private readonly ILlmClient _llmClient;
    private readonly IJarvisLogger _jarvisLogger;

    public RemoveVariableFromMemoryJarvisModule(IMemoryManager memoryManager, ILlmClient llmClient, IJarvisLogger jarvisLogger)
    {
        _memoryManager = memoryManager;
        _llmClient = llmClient;
        _jarvisLogger = jarvisLogger;
    }

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync()
    {
        var availableKeys = _memoryManager.ListKeys();
        string availableKeysStr = string.Join(", ", availableKeys);

        string selectKeyPrompt = $@"
<purpose>
    Select a key from the available keys in memory based on the user's prompt.
</purpose>

<instructions>
    <instruction>Based on the user's prompt and the list of available keys, infer which key the user wants to remove from memory.</instruction>
    <instruction>If no key matches, return an empty string for 'key'.</instruction>
</instructions>

<available-keys>
    {availableKeysStr}
</available-keys>

<user-prompt>
    {Prompt}
</user-prompt>
";

        MemoryKeyResponse keySelectionResponse =
            await _llmClient.StructuredOutputPrompt<MemoryKeyResponse>(selectKeyPrompt,
                Constants.ModelNameToId[ModelName.FastModel]);

        _jarvisLogger.LogInformation(
            $"Key selection response: {JsonConvert.SerializeObject(keySelectionResponse)}");

        if (string.IsNullOrEmpty(keySelectionResponse.Key))
        {
            return new Dictionary<string, object>
            {
                { "status", "not_found" },
                { "message", "No matching key found in memory" },
            };
        }

        if (_memoryManager.Delete(keySelectionResponse.Key))
        {
            return new Dictionary<string, object>
            {
                { "status", "success" },
                { "message", $"Key '{keySelectionResponse.Key}' removed from memory" },
            };
        }
        else
        {
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "message", $"Failed to remove key '{keySelectionResponse.Key}' from memory" },
            };
        }
    }
}