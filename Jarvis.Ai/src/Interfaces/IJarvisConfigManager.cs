namespace Jarvis.Ai.src.Interfaces;

public interface IJarvisConfigManager
{
    string? GetValue(string key);
    T? GetSection<T>(string sectionName) where T : class, new();
}