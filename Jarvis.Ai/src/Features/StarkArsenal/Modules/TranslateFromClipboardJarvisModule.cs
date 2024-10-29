using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Models;
using TextCopy;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Translates this text from clipboard to specified language.")]
public class TranslateFromClipboardJarvisModule : BaseJarvisModule
{
    [TacticalComponent("The user's prompt indicating target language and any style preferences.", "string")]
    public string Prompt { get; set; } = null;

    private readonly ILlmClient _llmClient;

    public TranslateFromClipboardJarvisModule(ILlmClient llmClient)
    {
        _llmClient = llmClient;
    }

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Get text from clipboard
            string? clipboardText = await ClipboardService.GetTextAsync();
            if (string.IsNullOrEmpty(clipboardText))
            {
                return new Dictionary<string, object>
                {
                    { "status", "error" },
                    { "message", "Clipboard is empty" }
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Get translation parameters
            string translationPrompt = $@"
<purpose>
    Translate the given text based on the analysis.
</purpose>

<instructions>
    <instruction>Translate the text while preserving original formatting.</instruction>
    <instruction>Maintain the context and style of the original text.</instruction>
    <instruction>Return only the translated text without any explanations or metadata.</instruction>
</instructions>

<text>
{clipboardText}
</text>

<text>
{clipboardText}
</text>
";

            cancellationToken.ThrowIfCancellationRequested();

            // Get translation
            string translatedText = await _llmClient.ChatPrompt(translationPrompt, Constants.ModelNameToId[ModelName.BaseModel]);

            return new Dictionary<string, object>
            {
                { "status", "success" },
                { "translated_text", translatedText }
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
                { "message", $"Translation failed: {e.Message}" }
            };
        }
    }
}
