using System.Text.Json;
using System.Xml.Linq;
using Jarvis.Ai.Common.Utils;
using Jarvis.Ai.Interfaces;

namespace Jarvis.Console;

public class MemoryManager : IMemoryManager
{
    private readonly string _filePath;
    private Dictionary<string, object> _memory = new();

    public MemoryManager(IJarvisConfigManager configManager)
    {
        var filePath = configManager.GetValue("ACTIVE_MEMORY_FILE");
        if (string.IsNullOrEmpty(filePath))
        {
            throw new Exception("ACTIVE_MEMORY_FILE environment variable not set.");
        }
        _filePath = filePath;
        LoadMemory();
    }

    public void LoadMemory()
    {
        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            _memory = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
        else
        {
            _memory = new Dictionary<string, object>();
        }
    }

    public void SaveMemory()
    {
        var json = JsonSerializer.Serialize(_memory, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    public bool Create(string key, object value)
    {
        if (!_memory.ContainsKey(key))
        {
            _memory[key] = value;
            SaveMemory();
            return true;
        }

        return false;
    }

    public object Read(string key)
    {
        return _memory.ContainsKey(key) ? _memory[key] : null;
    }

    public bool Update(string key, object value)
    {
        if (_memory.ContainsKey(key))
        {
            _memory[key] = value;
            SaveMemory();
            return true;
        }

        return false;
    }

    public bool Delete(string key)
    {
        if (_memory.ContainsKey(key))
        {
            _memory.Remove(key);
            SaveMemory();
            return true;
        }

        return false;
    }

    public List<string> ListKeys()
    {
        return new List<string>(_memory.Keys);
    }

    public bool Upsert(string key, object value)
    {
        _memory[key] = value;
        SaveMemory();
        return true;
    }

    public string GetXmlForPrompt(List<string> keys)
    {
        var root = new XElement("memory");
        bool matchedKeys = false;
        foreach (var pattern in keys)
        {
            foreach (var key in _memory.Keys)
            {
                if (Utils.MatchPattern(pattern, key))
                {
                    var child = new XElement(key)
                    {
                        Value = _memory[key].ToString()
                    };
                    root.Add(child);
                    matchedKeys = true;
                }
            }
        }

        return matchedKeys ? root.ToString() : "";
    }

    public void Reset()
    {
        _memory.Clear();
        SaveMemory();
    }
}