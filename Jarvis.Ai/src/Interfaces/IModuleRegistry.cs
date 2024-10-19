namespace Jarvis.Ai.Interfaces;

public interface IModuleRegistry
{
    Task<object> ExecuteCommand(string name, Dictionary<string, object> args);
    void RegisterCommandsFromAssembly();
}