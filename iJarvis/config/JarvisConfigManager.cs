using Jarvis.Ai.Interfaces;

namespace Jarvis.Service.Config;

public class JarvisConfigManager : IJarvisConfigManager
{
    private static readonly IConfiguration Configuration;

    static JarvisConfigManager()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        Configuration = builder.Build();
    }

    public string? GetValue(string key)
    {
        return Configuration[key];
    }

    public T? GetSection<T>(string sectionName) where T : class, new()
    {
        return Configuration.GetSection(sectionName).Get<T>();
    }
}