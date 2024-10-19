namespace Jarvis.Ai.Interfaces
{
    public interface ICommand
    {
        Task<object> Execute(Dictionary<string, object> args);
    }
}
