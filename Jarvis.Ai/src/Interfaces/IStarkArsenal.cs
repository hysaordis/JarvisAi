using OllamaSharp.Models.Chat;

namespace Jarvis.Ai.Interfaces;

public interface IStarkArsenal
{
    List<object> GetTacticalArray();
    List<Tool> GetToolsForOllama();
}