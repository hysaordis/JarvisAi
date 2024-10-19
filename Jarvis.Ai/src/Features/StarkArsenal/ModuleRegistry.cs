using System.Reflection;
using Jarvis.Ai.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Jarvis.Ai.Features.StarkArsenal;

public class ModuleRegistry : IModuleRegistry
{
    private readonly Dictionary<string, IJarvisModule> _commands = new();
    private readonly IServiceProvider _serviceProvider;

    public ModuleRegistry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private void RegisterCommand(string name, IJarvisModule jarvisModule)
    {
        _commands[name.ToLower()] = jarvisModule;
    }

    public async Task<object> ExecuteCommand(string name, Dictionary<string, object> args)
    {
        if (_commands.TryGetValue(name.ToLower(), out var command))
        {
            return await command.Execute(args);
        }
        throw new KeyNotFoundException($"Command '{name}' not found.");
    }

    public void RegisterCommandsFromAssembly()
    {
        var commandTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IJarvisModule).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

        foreach (var commandType in commandTypes)
        {
            var command = (IJarvisModule)ActivatorUtilities.CreateInstance(_serviceProvider, commandType);
            RegisterCommand(commandType.Name, command);
        }
    }
}