using System.Text.Json;
using Jarvis.Ai.Interfaces;

namespace Jarvis.Ai.Common.Settings;

public class AIInstructionsConfig
{
    public string[] CORE_INSTRUCTIONS { get; set; } = Array.Empty<string>();

    public string GetFormattedInstructions()
    {
        return string.Join("\n", CORE_INSTRUCTIONS);
    }
}

public class StarkProtocols
{
    public string AiAssistantName { get; private set; }
    private string? HumanName { get; set; }
    public string SessionInstructions { get; private set; }
    public string? Voice { get; set; }
    public const int PrefixPaddingMs = 300;
    public const double SilenceThreshold = 0.5;
    public const int SilenceDurationMs = 700;
    private readonly Dictionary<string, object>? Personalization;
    private readonly AIInstructionsConfig _aiInstructions;

    public StarkProtocols(IJarvisConfigManager jarvisConfigManager)
    {
        _aiInstructions = jarvisConfigManager.GetSection<AIInstructionsConfig>("AI_INSTRUCTIONS") ?? new AIInstructionsConfig();

        string? personalizationFile = jarvisConfigManager.GetValue("PERSONALIZATION_FILE");
        if (File.Exists(personalizationFile))
        {
            string json = File.ReadAllText(personalizationFile);
            Personalization = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            AiAssistantName = (Personalization!.ContainsKey("ai_assistant_name")
                ? Personalization["ai_assistant_name"].ToString()
                : "Assistant")!;
            HumanName = Personalization.TryGetValue("human_name", out var humanName) ? humanName.ToString() : "User";
            Voice = Personalization.TryGetValue("voice", out var voice) ? voice.ToString() : "alloy";
        }
        else
        {
            AiAssistantName = "Assistant";
            HumanName = "User";
        }

        SessionInstructions = $"You are {AiAssistantName}, the AI assistant to {HumanName}.\n" +
                            $"{GetCompleteInstructions()}";
    }

    private string GetCompleteInstructions()
    {
        return _aiInstructions.GetFormattedInstructions();
    }

    public List<string>? GetBrowserUrls()
    {
        if (Personalization != null && Personalization.TryGetValue("browser_urls", out var browserUrls))
        {
            return JsonSerializer.Deserialize<List<string>>(browserUrls.ToString()!);
        }
        return new List<string>();
    }

    public string GetFocusFile()
    {
        if (Personalization != null && Personalization.TryGetValue("focus_file", out var focusfile))
        {
            return focusfile.ToString()!;
        }
        return null;
    }
}