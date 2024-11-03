using System.Reflection;
using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Features.StarkArsenal;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.LLM;
using Jarvis.Ai.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jarvis.Ai;

/// <summary>
/// The StarkFleetAssembler class is responsible for configuring and assembling
/// the various services and modules that make up the Jarvis AI system.
/// </summary>
public static class StarkFleetAssembler
{
    /// <summary>
    /// Configures and registers the necessary services and modules for the Jarvis AI system.
    /// </summary>
    /// <param name="services">The IServiceCollection to which services are added.</param>
    /// <returns>The updated IServiceCollection.</returns>
    public static IServiceCollection AssembleJarvisSystems(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration _configuration)
    {
        var transcriberType = _configuration.GetValue<string>("LLM_TYPE") ?? "ollama";

        switch (transcriberType)
        {
            case "ollama":
                services.AddSingleton<ILlmClient, OllamaLlmClient>();
                break;
            case "openai":
                services.AddSingleton<ILlmClient, OpenAiLlmClient>();
                break;
            default:
                services.AddSingleton<ILlmClient, OllamaLlmClient>();
                break;
        }

        // Register LLM Factory and Client
        // All Modules that Jarvis can use
        // Register the module registry as a singleton service
        services.AddSingleton<IModuleRegistry, ModuleRegistry>();

        // All the conversations that Jarvis has had
        // Register the conversation store as a singleton service
        services.AddSingleton<IConversationStore, ConversationStore>();

        // Initialize the StarkProtocols example: 
        // System prompts for the AI assistant
        // Register the StarkProtocols as a singleton service
        services.AddSingleton<StarkProtocols>();

        // Load all the tactical modules to be used by the AI assistant for various tasks
        var tacticalModules = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IJarvisModule).IsAssignableFrom(t) && !t.IsAbstract);
        foreach (var module in tacticalModules)
        {
            services.AddTransient(typeof(IJarvisModule), module);
        }

        // Initialize the StarkArsenal with the tactical modules
        // To be used by the AI assistant for various tasks
        // Register the StarkArsenal with the tactical modules as a singleton service
        services.AddSingleton<IStarkArsenal>(sp => new StarkArsenal(tacticalModules));

        return services;
    }
}