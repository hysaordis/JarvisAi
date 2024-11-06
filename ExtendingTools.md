# Extending and Creating Tools for Jarvis.AI

This guide provides instructions and examples on how to extend the functionality of Jarvis.AI by creating new tools or modifying existing ones.

## Creating a New Tool

To create a new tool for Jarvis.AI, follow these steps:

1. Create a new class that inherits from `BaseJarvisModule` in the `Jarvis.Ai.Features.StarkArsenal.Modules` namespace.
2. Add the `JarvisTacticalModule` attribute to the class, providing a description of the tool's functionality.
3. Define the necessary properties for the tool and decorate them with the `TacticalComponent` attribute, specifying the description, type, and whether it is required.
4. Implement the `ExecuteComponentAsync` method, which contains the logic for the tool's execution.
5. Use the provided interfaces and services, such as `IJarvisConfigManager`, `LlmClient`, and `IFileManager`, to interact with the system and perform the desired operations.
6. Return a dictionary with the results or status of the tool's execution.

Here's an example of a new tool that summarizes a text document based on the user's prompt:

```csharp
using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.Models;
using Jarvis.Ai.src.Interfaces;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Provides a summary of a text document based on the user's prompt.")]
public class SummarizeTextJarvisModule : BaseJarvisModule
{
    [TacticalComponent("The user's prompt describing what kind of summary is needed.", "string", true)]
    public string Prompt { get; set; }

    [TacticalComponent("The model to use for summarizing the text. Defaults to 'BaseModel' if not explicitly specified.", "string")]
    public string Model { get; set; } = ModelName.BaseModel.ToString();
  
    private readonly IJarvisConfigManager _jarvisConfigManager;
    private readonly LlmClient _llmClient;
    private readonly IFileManager _fileManager;

    public SummarizeTextJarvisModule(IJarvisConfigManager jarvisConfigManager, LlmClient llmClient, IFileManager fileManager)
    {
        _jarvisConfigManager = jarvisConfigManager;
        _llmClient = llmClient;
        _fileManager = fileManager;
    }

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync()
    {
        // Implementation of the tool's logic
        // ...
    }
}
```

## Modifying an Existing Tool

To modify an existing tool, locate the corresponding class in the `Jarvis.Ai.Features.StarkArsenal.Modules` namespace and make the necessary changes to the properties, attributes, or the `ExecuteComponentAsync` method.

Remember to update the `JarvisTacticalModule` and `TacticalComponent` attributes if the functionality or parameters of the tool have changed.

## Testing and Debugging

When creating or modifying tools, it's important to test and debug them thoroughly. Use the provided logging mechanisms, such as `IJarvisLogger`, to log relevant information and errors during the tool's execution.

You can also use the development mode of Jarvis.AI to test your tools interactively and see the results in real-time.

## Best Practices

- Keep the tools focused and modular, with each tool performing a specific task.
- Use meaningful names for the tool classes and properties to enhance readability and maintainability.
- Leverage the provided interfaces and services to interact with the system and ensure compatibility.
- Handle errors and edge cases gracefully, providing informative error messages or fallback values when necessary.
- Document the functionality, parameters, and usage of the tools using the `JarvisTacticalModule` and `TacticalComponent` attributes.

By following these guidelines and examples, you can easily extend the capabilities of Jarvis.AI by creating new tools or modifying existing ones to suit your specific needs.
