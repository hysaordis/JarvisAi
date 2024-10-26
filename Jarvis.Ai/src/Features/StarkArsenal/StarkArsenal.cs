using System.Reflection;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;
using OllamaSharp.Models.Chat;

namespace Jarvis.Ai.Features.StarkArsenal;

public class StarkArsenal : IStarkArsenal
{
    private readonly IEnumerable<Type> _tacticalModules;

    public StarkArsenal(IEnumerable<Type> tacticalModules)
    {
        _tacticalModules = tacticalModules;
    }

    public List<Tool> GetToolsForOllama()
    {
        var toolLists = new List<Tool>();

        foreach (var module in _tacticalModules)
        {
            var moduleSpecs = module.GetCustomAttribute<JarvisTacticalModuleAttribute>();
            if (moduleSpecs == null) continue;

            var moduleName = module.Name;

            var moduleParameters = new Dictionary<string, Properties>();
            var requiredParameters = new List<string>();

            foreach (var component in module.GetProperties())
            {
                var componentSpecs = component.GetCustomAttribute<TacticalComponentAttribute>();
                if (componentSpecs == null) continue;

                moduleParameters[component.Name] = new Properties
                {
                    Type = componentSpecs.Type,
                    Description = componentSpecs.Description
                };

                if (componentSpecs.IsRequired)
                {
                    requiredParameters.Add(component.Name);
                }
            }

            var function = new Function
            {
                Name = moduleName,
                Description = moduleSpecs.Description,
                Parameters = new Parameters
                {
                    Type = "object",
                    Properties = moduleParameters,
                    Required = requiredParameters
                }
            };

            toolLists.Add(new Tool()
            {
                Function = function,
            });
        }

        return toolLists;
    }

    public List<object> GetTacticalArray()
    {
        var tacticalArray = new List<object>();

        foreach (var module in _tacticalModules)
        {
            var moduleSpecs = module.GetCustomAttribute<JarvisTacticalModuleAttribute>();
            if (moduleSpecs == null) continue;

            var moduleName = module.Name;

            var moduleParameters = new Dictionary<string, object>();
            var requiredParameters = new List<string>();

            foreach (var component in module.GetProperties())
            {
                var componentSpecs = component.GetCustomAttribute<TacticalComponentAttribute>();
                if (componentSpecs == null) continue;

                moduleParameters[component.Name] = new
                {
                    type = componentSpecs.Type,
                    description = componentSpecs.Description
                };

                if (componentSpecs.IsRequired)
                {
                    requiredParameters.Add(component.Name);
                }
            }

            tacticalArray.Add(new
            {
                type = "function",
                name = moduleName,
                description = moduleSpecs.Description,
                parameters = new
                {
                    type = "object",
                    properties = moduleParameters,
                    required = requiredParameters
                }
            });
        }

        return tacticalArray;
    }
}