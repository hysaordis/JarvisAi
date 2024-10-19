using System.Reflection;
using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Features.DiagramGeneration;
using Jarvis.Ai.Features.StarkArsenal;
using Jarvis.Ai.Features.WebDataExtraction;
using Jarvis.Ai.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Jarvis.Ai;

public static class StarkFleetAssembler
{
    public static IServiceCollection AssembleJarvisSystems(this IServiceCollection services)
    {
        services.AddSingleton<IModuleRegistry, ModuleRegistry>();
        services.AddSingleton<LlmClient>();
        services.AddSingleton<FireCrawlApp>();
        services.AddSingleton<DiagramGenerationTool>();
        services.AddSingleton<StarkProtocols>();

        var tacticalModules = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IJarvisModule).IsAssignableFrom(t) && !t.IsAbstract);
        foreach (var module in tacticalModules)
        {
            services.AddTransient(typeof(IJarvisModule), module);
        }

        services.AddSingleton<IStarkArsenal>(sp => new StarkArsenal(tacticalModules));

        return services;
    }
}