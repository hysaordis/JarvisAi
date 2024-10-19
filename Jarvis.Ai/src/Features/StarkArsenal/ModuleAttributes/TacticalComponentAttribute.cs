namespace Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;

[AttributeUsage(AttributeTargets.Property)]
public class TacticalComponentAttribute : Attribute
{
    public string Description { get; }
    public string Type { get; }
    public bool IsRequired { get; }

    public TacticalComponentAttribute(string description, string type, bool isRequired = false)
    {
        Description = description;
        Type = type;
        IsRequired = isRequired;
    }
}