using System.Reflection;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jarvis.Ai.Features.StarkArsenal;

public class ModuleRegistry : IModuleRegistry
{
    private readonly Dictionary<string, IJarvisModule> _commands = new();
    private readonly Dictionary<string, FunctionDefinition> _functionDefinitions = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ModuleRegistry> _logger;
    private readonly List<(Type Type, Exception Exception)> _failedModules = new();

    public ModuleRegistry(
        IServiceProvider serviceProvider,
        ILogger<ModuleRegistry> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        RegisterCommandsFromAssembly();
    }

    private void RegisterCommand(string name, IJarvisModule jarvisModule)
    {
        _commands[name.ToLower()] = jarvisModule;
        _functionDefinitions[name.ToLower()] = GetFunctionDefinitionFromModule(jarvisModule);
    }

    public async Task<object> ExecuteCommand(string name, Dictionary<string, object> args, CancellationToken cancellationToken = default)
    {
        if (_commands.TryGetValue(name.ToLower(), out var command))
        {
            return await command.Execute(args, cancellationToken);
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
            try
            {
                var command = (IJarvisModule)ActivatorUtilities.CreateInstance(_serviceProvider, commandType);
                RegisterCommand(commandType.Name, command);
                _logger.LogInformation("Successfully registered module: {ModuleName}", commandType.Name);
            }
            catch (Exception ex)
            {
                _failedModules.Add((commandType, ex));
                _logger.LogError(ex, "Failed to register module {ModuleName}. Module will be skipped.", commandType.Name);
            }
        }

        if (_failedModules.Any())
        {
            _logger.LogWarning("Some modules failed to load. Total failed modules: {Count}", _failedModules.Count);
        }
    }

    // Metodo opzionale per diagnostica
    public IReadOnlyList<(Type Type, Exception Exception)> GetFailedModules() => _failedModules.AsReadOnly();
}

public class FunctionDefinition
{
    public string Name { get; set; }
    public List<string> Parameters { get; set; }
}