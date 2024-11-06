using Jarvis.Ai.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace Jarvis.Service.Config;

public class JarvisConfigManager : IJarvisConfigManager
{
    private static readonly IConfiguration Configuration;

    static JarvisConfigManager()
    {
        var builder = new ConfigurationBuilder();
        
        var isWindowsService = WindowsServiceHelpers.IsWindowsService();
        
        if (isWindowsService)
        {
            var programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var servicePath = Path.Combine(programDataPath, "iJarvis");
            builder.SetBasePath(servicePath);
            
            builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        }
        else
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            builder.SetBasePath(currentDirectory);
            
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            
            builder.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
        }

        builder.AddEnvironmentVariables();

        try
        {
            Configuration = builder.Build();
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error building configuration. IsWindowsService: {isWindowsService}, " +
                             $"CurrentDirectory: {Directory.GetCurrentDirectory()}, " +
                             $"Error: {ex.Message}";
            
            if (isWindowsService)
            {
                using var eventLog = new System.Diagnostics.EventLog("Application");
                eventLog.Source = "iJarvis Service";
                eventLog.WriteEntry(errorMessage, System.Diagnostics.EventLogEntryType.Error);
            }
            else
            {
                Console.Error.WriteLine(errorMessage);
            }
            
            throw;
        }
    }

    public string? GetValue(string key)
    {
        var value = Configuration[key];
        if (value == null)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var isService = WindowsServiceHelpers.IsWindowsService();
            Console.WriteLine($"Warning: Configuration key '{key}' not found. Environment: {environment}, IsService: {isService}");
        }
        return value;
    }

    public T? GetSection<T>(string sectionName) where T : class, new()
    {
        var section = Configuration.GetSection(sectionName).Get<T>();
        if (section == null)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var isService = WindowsServiceHelpers.IsWindowsService();
            Console.WriteLine($"Warning: Configuration section '{sectionName}' not found. Environment: {environment}, IsService: {isService}");
        }
        return section;
    }

    public string GetConversationStoragePath()
    {
        var path = Configuration["ConversationStoragePath"];
        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidOperationException("Conversation storage path is not configured.");
        }
        return path;
    }
}