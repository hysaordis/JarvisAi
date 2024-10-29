using Jarvis.Ai;
using Jarvis.Ai.Features.AudioProcessing;
using Jarvis.Ai.Features.VisualOutput;
using Jarvis.Ai.Interfaces;
using Jarvis.Console.config;
using Microsoft.Extensions.DependencyInjection;

namespace Jarvis.Console;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register other services
        services.AddSingleton<IJarvisConfigManager, JarvisConfigManager>();
        services.AddSingleton<IJarvisLogger, Logger>();
        services.AssembleJarvisSystems();
        services.AddSingleton<IVoiceInputModule, VoiceInputModule>();
        services.AddSingleton<IAudioOutputModule, AudioOutputModule>();
        services.AddSingleton<IDisplayModule, VisualInterfaceModule>();
        services.AddSingleton<IMemoryManager, MemoryManager>();

        //services.AddSingleton<ITranscriber, WhisperTranscriber>();
        services.AddSingleton<ITranscriber, AssemblyAITranscriber>();
        //services.AddSingleton<ITranscriber, AssemblyAIRealtimeTranscriber>();

        //services.AddSingleton<IJarvis, JarvisAgent>();/
        services.AddSingleton<IJarvis, AlitaAgent>();//

        // Register IronManSuit which depends on IJarvis
        services.AddSingleton<IronManSuit>();
    }

    public void Configure(IServiceProvider serviceProvider)
    {
        var moduleRegistry = serviceProvider.GetRequiredService<IModuleRegistry>();
        moduleRegistry.RegisterCommandsFromAssembly();
    }
}