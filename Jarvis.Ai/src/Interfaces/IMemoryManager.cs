namespace Jarvis.Ai.Interfaces;

public interface IMemoryManager
{
    void LoadMemory();
    void SaveMemory();
    bool Create(string key, object value);
    object Read(string key);
    bool Update(string key, object value);
    bool Delete(string key);
    List<string> ListKeys();
    bool Upsert(string key, object value);
    string GetXmlForPrompt(List<string> keys);
    void Reset();
}