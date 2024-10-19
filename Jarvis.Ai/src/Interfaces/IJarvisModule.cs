namespace Jarvis.Ai.Interfaces;

public interface IJarvisModule
{
    Task<Dictionary<string, object>> Execute(Dictionary<string, object> args);
}