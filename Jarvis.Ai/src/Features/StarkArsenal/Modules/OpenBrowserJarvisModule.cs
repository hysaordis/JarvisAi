using System.Diagnostics;
using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.Models;
using Newtonsoft.Json;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Opens a browser tab with the best-fitting URL based on the user's prompt.")]
public class OpenBrowserJarvisModule : BaseJarvisModule
{
    [TacticalComponent("The user's prompt to determine which URL to open.", "string", true)]
    public string Prompt { get; set; }

    private readonly StarkProtocols _starkProtocols;
    private readonly ILlmClient _llmClient;
    private readonly IJarvisLogger _jarvisLogger;

    public OpenBrowserJarvisModule(StarkProtocols starkProtocols, ILlmClient llmClient, IJarvisLogger jarvisLogger)
    {
        _starkProtocols = starkProtocols;
        _llmClient = llmClient;
        _jarvisLogger = jarvisLogger;
    }

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var browserUrls = _starkProtocols.GetBrowserUrls();
            string browserUrlsStr = string.Join(", ", browserUrls);

            string promptStructure = $@"
<purpose>
    Select a browser URL from the list of browser URLs based on the user's prompt.
</purpose>

<instructions>
    <instruction>Infer the browser URL that the user wants to open from the user-prompt and the list of browser URLs.</instruction>
    <instruction>If the user-prompt is not related to the browser URLs, return an empty string.</instruction>
</instructions>

<browser-urls>
    {browserUrlsStr}
</browser-urls>

<user-prompt>
    {Prompt}
</user-prompt>
";

            _jarvisLogger.LogInformation($"📖 open_browser() Prompt: {promptStructure}");

            cancellationToken.ThrowIfCancellationRequested();

            WebUrl response =
                await _llmClient.StructuredOutputPrompt<WebUrl>(promptStructure,
                    Constants.ModelNameToId[ModelName.FastModel]);

            _jarvisLogger.LogInformation($"📖 open_browser() Response: {JsonConvert.SerializeObject(response)}");

            if (!string.IsNullOrEmpty(response.Url))
            {
                _jarvisLogger.LogInformation($"📖 open_browser() Opening URL: {response}");
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = response.Url,
                        UseShellExecute = true
                    });
                    return new Dictionary<string, object>
                    {
                        { "status", "Browser opened" },
                        { "url", response },
                    };
                }
                catch (Exception e)
                {
                    _jarvisLogger.LogError($"Failed to open browser: {e.Message}");
                    return new Dictionary<string, object>
                    {
                        { "status", "Error" },
                        { "message", $"Failed to open browser: {e.Message}" },
                    };
                }
            }

            return new Dictionary<string, object>
            {
                { "status", "No URL found" },
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
                { "message", $"Failed to process browser request: {e.Message}" }
            };
        }
    }
}
