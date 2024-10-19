![Jarvis AI](image.webp)

# Jarvis.AI

Jarvis.AI is an AI assistant inspired by the AI from the Iron Man series. Built using C#, it leverages the power of OpenAI's API to offer a multifunctional assistant capable of various tasks. This project aims to provide a useful and customizable assistant while acknowledging the contributions of the underlying OpenAI technology.

## 🌟 Features

- **Voice Interaction**: Voice input and output for natural communication.
- **Visual Interface**: Graphical user interface for enhanced interaction and data visualization.
- **WebSocket Communication**: Real-time communication with OpenAI's API.
- **Modular Architecture**: Well-designed architecture allows easy extension of Jarvis modules.
  1. Use the same technique as existing modules for reference.
  2. Each module can be placed in the same directory as other modules for brevity.
  3. Each module must inherit from `BaseJarvisModule`.
  4. Each module must have a `JarvisTacticalModule` attribute at the class level and one or more `TacticalComponent` attributes for parameters. These attributes help the automatic dependency injection system import all modules correctly without needing further modifications to the system.

  ```csharp
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
  ```
- **Persistent Memory**: Stores context for personalized interactions.
- **Tool Integration**: Wide range of tools for various tasks.
- **CLI Support**: Command-line interaction for quick operations.

## 🛠️ Technologies Used

- C# and .NET
- WebSocket (real-time communication)
- OpenAI API
- JSON (data serialization/deserialization)

## 🤖 Assistant Tools Overview

Jarvis.AI includes various tools to enhance functionality:

### Utility Functions
- Current time, random number generation, browser tab management, Mermaid diagram generation.

### File Operations
- Create, update, delete, read files and directories, discuss contents.

### Memory Management
- Add, remove, reset variables in memory, clipboard operations.

### Information Sourcing
- Web scraping capabilities.

### Diagram Generation
- Create Mermaid diagrams based on prompts.

## 🚀 Getting Started

(Instructions for setting up and running the project go here)

## 🖥️ CLI Usage

Interact with Jarvis.AI using the command-line interface. Use the `--prompts` flag to issue commands, and chain multiple commands with `|`.

### CLI Examples

1. Simple greeting:
   ```
   dotnet run --project iJarvis -- --prompts "Hello, how are you?"
   ```

2. Multiple commands:
   ```
   dotnet run --project iJarvis -- --prompts "Command 1 | Command 2 | Command 3"
   ```

3. Open a website:
   ```
   dotnet run --project iJarvis -- --prompts "Open Hacker News"
   ```

4. Memory operations:
   ```
   dotnet run --project iJarvis -- --prompts "copy my current clipboard to memory"
   dotnet run --project iJarvis -- --prompts "add to memory the key 'project_status' with the value 'in progress'"
   dotnet run --project iJarvis -- --prompts "reset active memory"
   ```

5. File operations:
   ```
   dotnet run --project iJarvis -- --prompts "Create a new CSV file called user analytics with 10 mock rows."
   dotnet run --project iJarvis -- --prompts "read file user analytics into memory"
   ```

6. Web scraping:
   ```
   dotnet run --project iJarvis -- --prompts "scrape the URL from my clipboard and save it to a file"
   ```

7. Generating diagrams:
   ```
   dotnet run --project iJarvis -- --prompts "Generate a diagram outlining the architecture of a minimal TikTok clone"
   ```

## 🗣️ Voice Commands Examples

- "Jarvis, what's the current time?"
- "Open ChatGPT, Claude, and Hacker News."
- "Generate a diagram outlining the architecture of a minimal TikTok clone."

## 📋 Supported Commands

**Memory Operations**: Add/remove variables, clipboard to memory, reset memory.

**File Operations**: Create, delete, update, read files, discuss file contents.

**Other Features**: Get current time, generate random numbers, open browser tabs, web scraping, generate diagrams.

## 💡 Key Components

- **IronManSuit**: Main class for initializing and managing core components.

### Example Module Implementation
Below is an example of how to create a new module for Jarvis.AI:

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
    public string Model { get; set; }
        
    private readonly IJarvisConfigManager _jarvisConfigManager;
    private readonly LlmClient _llmClient;
    private readonly IFileManager _fileManager;

    public SummarizeTextJarvisModule(IJarvisConfigManager jarvisConfigManager, LlmClient llmClient, IFileManager fileManager)
    {
        _jarvisConfigManager = jarvisConfigManager;
        _llmClient = llmClient;
        _fileManager = fileManager;
    }

    protected override async Task<Dictionary<string, object>> ExecuteInternal(Dictionary<string, object> args)
    {
        string prompt = args["Prompt"].ToString();
        string model = args.ContainsKey("model") ? args["model"].ToString() : ModelName.BaseModel.ToString();
        string filePath = _jarvisConfigManager.GetValue("TEXT_FILE_PATH");

        if (string.IsNullOrEmpty(filePath) || !_fileManager.FileExists(filePath))
        {
            return new Dictionary<string, object>
            {
                { "status", "File not found" },
                { "file_path", filePath }
            };
        }

        string fileContent = await _fileManager.ReadFileAsync(filePath);
        string summarizePrompt = $@"
<purpose>
    Summarize the content of the file based on the user's prompt.
</purpose>

<instructions>
    <instruction>Based on the user's prompt and the file content, provide a concise summary.</instruction>
    <instruction>Be concise and focus on key information that matches the user's prompt.</instruction>
</instructions>

<file-content>
{fileContent}
</file-content>

<user-prompt>
{prompt}
</user-prompt>
";

        string modelId = Constants.ModelNameToId[Enum.Parse<ModelName>(model)];
        string summary = await _llmClient.ChatPrompt(summarizePrompt, modelId);

        return new Dictionary<string, object>
        {
            { "status", "Text summarized" },
            { "file_path", filePath },
            { "summary", summary }
        };
    }
}
```

- **JarvisAgent**: Manages WebSocket communication with OpenAI and conversation flow.
- **StarkIndustries**: Tool definitions and implementations.

## 🌈 Inspiration

This project draws inspiration from a Python-based AI assistant by [disler](https://github.com/disler/poc-realtime-ai-assistant). For more tutorials, check out [Indy Dev Dan's YouTube channel](https://www.youtube.com/@indydevdan).

## 📘 Documentation

(Information about where to find detailed documentation goes here)

## PROJECT_STRUCTURE

The project is organized as follows: [Detailed Project Structure](PROJECT_STRUCTURE.md)

- **Jarvis.Ai.Common**: Contains common settings and utilities used across the project.
- **Jarvis.Ai.Features.StarkArsenal**: Contains all modules and features that extend Jarvis's capabilities.
  - **ModuleAttributes**: Defines attributes like `TacticalComponentAttribute` and `JarvisTacticalModuleAttribute` that are used to mark properties and classes for dependency injection.
  - **Modules**: Includes individual module implementations like `DiscussFileJarvisModule` and `SummarizeTextJarvisModule`.
- **Jarvis.Ai.Interfaces**: Contains interfaces used for dependency injection, allowing different components to interact seamlessly.
- **Jarvis.Ai.Models**: Contains data models used throughout the system.
- **Jarvis.Ai.src.Interfaces**: Includes additional interfaces specific to core functionalities of Jarvis.

Each module is placed in the **Modules** folder within **StarkArsenal**, following the same structure to ensure consistency and ease of extension. New modules should inherit from `BaseJarvisModule` and use the attributes provided to facilitate the automatic dependency injection.

## 🤝 Contributing

We welcome contributions to Jarvis.AI! Please read our contributing guidelines to get started.

## 📄 License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## 📞 Contact

For any inquiries or contributions, contact:

Ordis Hysa
- Email: [ordishysa@gmail.com](mailto:ordishysa@gmail.com)
- [LinkedIn](https://www.linkedin.com/in/ordishysa/)

---

Built with ❤️ by Ordis Hysa

---
