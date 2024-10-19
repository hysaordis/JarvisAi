namespace Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class JarvisTacticalModuleAttribute : Attribute
{
    public string Description { get; }

    public JarvisTacticalModuleAttribute(string description)
    {
        Description = description;
    }
}