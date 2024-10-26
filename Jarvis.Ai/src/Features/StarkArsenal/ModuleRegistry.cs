using System.Reflection;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Jarvis.Ai.Features.StarkArsenal;

public class ModuleRegistry : IModuleRegistry
{
    private readonly Dictionary<string, IJarvisModule> _commands = new();
    private readonly Dictionary<string, FunctionDefinition> _functionDefinitions = new();
    private readonly IServiceProvider _serviceProvider;

    public ModuleRegistry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        RegisterCommandsFromAssembly();
    }

    private void RegisterCommand(string name, IJarvisModule jarvisModule)
    {
        _commands[name.ToLower()] = jarvisModule;
        _functionDefinitions[name.ToLower()] = GetFunctionDefinitionFromModule(jarvisModule);
    }

    public async Task<object> ExecuteCommand(string name, Dictionary<string, object> args)
    {
        if (_commands.TryGetValue(name.ToLower(), out var command))
        {
            return await command.Execute(args);
        }
        throw new KeyNotFoundException($"Command '{name}' not found.");
    }

    private FunctionDefinition GetFunctionDefinitionFromModule(IJarvisModule module)
    {
        var type = module.GetType();
        var parameters = new List<string>();

        foreach (var prop in type.GetProperties())
        {
            var attribute = prop.GetCustomAttribute<TacticalComponentAttribute>();
            if (attribute != null)
            {
                parameters.Add(prop.Name);
            }
        }

        return new FunctionDefinition
        {
            Name = type.Name,
            Parameters = parameters
        };
    }

    public void RegisterCommandsFromAssembly()
    {
        var commandTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IJarvisModule).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var commandType in commandTypes)
        {
            var command = (IJarvisModule)ActivatorUtilities.CreateInstance(_serviceProvider, commandType);
            RegisterCommand(commandType.Name, command);
        }
    }
}

public class FunctionDefinition
{
    public string Name { get; set; }
    public List<string> Parameters { get; set; }
}