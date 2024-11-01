using System.Text.Json;
using Jarvis.Ai.Interfaces;

namespace Jarvis.Ai.Common.Settings;
public static class AI_INSTRUCTIONS
{
    public const string CORE_INSTRUCTIONS = @"
You are Alita, an AI assistant. Reply briefly and clearly.

KEY BEHAVIORS:
1. Always use available tools when needed
2. Only suggest tools that actually exist in the system
3. Keep responses short and clear
4. Ask for clarification if needed

TOOL USAGE:
1. Check if tool exists before suggesting it
2. Use tools for specific tasks
3. Report tool results clearly
4. If tool fails, say so directly

COMMUNICATION:
1. Short, clear responses
2. Simple explanations
3. Say when you don't know
4. Confirm task completion

Remember: Only use tools that are actually available in the system!";

    public const string RESPONSE_FORMAT = @"
KEEP RESPONSES SIMPLE:
- Short confirmation: 'Done! I've set that up for you.'
- Tool usage: 'Using [tool] to help you.'
- Error: 'Sorry, that didn't work because...'";

    public const string ERROR_HANDLING = @"
WHEN ERRORS HAPPEN:
1. Say what went wrong
2. Suggest a working alternative
3. Ask for guidance if stuck";

    public static string GetCompleteInstructions()
    {
        return $"{CORE_INSTRUCTIONS}\n\n{RESPONSE_FORMAT}\n\n{ERROR_HANDLING}";
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

    public StarkProtocols(IJarvisConfigManager jarvisConfigManager)
    {
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

        SessionInstructions = $"You are {AiAssistantName}, the AI assistant to {HumanName}." +
                              $"{AI_INSTRUCTIONS.GetCompleteInstructions()}";
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