using Jarvis.Ai;
using Jarvis.Ai.Features.AudioProcessing;
using Jarvis.Ai.Features.VisualOutput;
using Jarvis.Ai.Interfaces;
using Jarvis.Console.config;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Jarvis.Console;

public class Startup
{
    public Startup()
    {
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // Register configuration and logging services
        services.AddSingleton<IJarvisConfigManager, JarvisConfigManager>();
        services.AddSingleton<IJarvisLogger, Logger>();

        // Get config manager to use for configuration
        var serviceProvider = services.BuildServiceProvider();
        var configManager = serviceProvider.GetRequiredService<IJarvisConfigManager>();

        // Register core Jarvis systems
        services.AssembleJarvisSystems();

        // Register input/output modules
        services.AddSingleton<IVoiceInputModule, VoiceInputModule>();
        // services.AddSingleton<IAudioOutputModule, AudioOutputModule>();
        services.AddSingleton<IAudioOutputModule, WindowsAudioOutputModule>();
        services.AddSingleton<IDisplayModule, VisualInterfaceModule>();

        // Register memory management
        services.AddSingleton<IMemoryManager, MemoryManager>();

        // Register transcribers based on configuration from config manager
        var transcriberType = configManager.GetValue("TranscriberType");
        switch (transcriberType)
        {
            case "3":
                services.AddSingleton<ITranscriber, AssemblyAIRealtimeTranscriber>();
                break;
            case "2":
                services.AddSingleton<ITranscriber, AssemblyAITranscriber>();
                break;
            case "1":
            default:
                services.AddSingleton<ITranscriber, WhisperTranscriber>();
                break;
        }

        // Register the main AI agent (only one should be active at a time)
        //services.AddSingleton<IJarvis, JarvisAgent>();
        services.AddSingleton<IJarvis, AlitaAgent>();

        // Register IronManSuit which depends on IJarvis
        services.AddSingleton<IronManSuit>();
    }

    public void Configure(IServiceProvider serviceProvider)
    {
        var moduleRegistry = serviceProvider.GetRequiredService<IModuleRegistry>();
        moduleRegistry.RegisterCommandsFromAssembly();
    }
}