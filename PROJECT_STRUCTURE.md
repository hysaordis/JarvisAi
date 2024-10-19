# Jarvis.AI Project Structure

This document provides an overview of the Jarvis.AI project structure and highlights important components.

## Project Overview

Jarvis.AI is a C# .NET project that implements an advanced AI assistant. The project is divided into two main parts:

1. `iJarvis`: The console application that serves as the entry point for the AI assistant.
2. `Jarvis.Ai`: The core library containing the main functionality of the AI assistant.

## Directory Structure

```
.
├── AlitaAI.sln
├── iJarvis/
│   ├── appsettings.json
│   ├── appsettings.local.json
│   ├── iJarvis.csproj
│   ├── Logger.cs
│   ├── MemoryManager.cs
│   ├── Program.cs
│   ├── Startup.cs
│   └── config/
│       ├── active_memory.json
│       ├── JarvisConfigManager.cs
│       └── personalization.json
└── Jarvis.Ai/
    ├── Jarvis.Ai.csproj
    ├── README.md
    └── src/
        ├── IronManSuit.cs
        ├── JarvisAgent.cs
        ├── LLMClient.cs
        ├── StarkFleetAssembler.cs
        ├── Common/
        │   ├── Settings/
        │   │   ├── Constants.cs
        │   │   └── StarkProtocols.cs
        │   └── Utils/
        │       └── Utils.cs
        ├── Features/
        │   ├── AudioProcessing/
        │   │   ├── AudioOutputModule.cs
        │   │   └── VoiceInputModule.cs
        │   ├── DiagramGeneration/
        │   │   └── DiagramGenerationTool.cs
        │   ├── StarkArsenal/
        │   │   ├── BaseJarvisModule.cs
        │   │   ├── ModuleRegistry.cs
        │   │   ├── StarkArsenal.cs
        │   │   ├── ModuleAttributes/
        │   │   │   ├── JarvisTacticalModuleAttribute.cs
        │   │   │   └── TacticalComponentAttribute.cs
        │   │   └── Modules/
        │   │       ├── AddToMemoryJarvisModule.cs
        │   │       ├── ClipboardToMemoryJarvisModule.cs
        │   │       ├── CreateFileJarvisModule.cs
        │   │       ├── DeleteFileJarvisModule.cs
        │   │       ├── DiscussFileJarvisModule.cs
        │   │       ├── GenerateDiagramJarvisModule.cs
        │   │       ├── GetCurrentTimeJarvisModule.cs
        │   │       ├── GetRandomNumberJarvisModule.cs
        │   │       ├── OpenBrowserJarvisModule.cs
        │   │       ├── ReadDirIntoMemoryJarvisModule.cs
        │   │       ├── ReadFileIntoMemoryJarvisModule.cs
        │   │       ├── RemoveVariableFromMemoryJarvisModule.cs
        │   │       ├── ResetActiveMemoryJarvisModule.cs
        │   │       ├── ScrapToFileFromClipboardJarvisModule.cs
        │   │       └── UpdateFileJarvisModule.cs
        │   ├── VisualOutput/
        │   │   └── VisualInterfaceModule.cs
        │   └── WebDataExtraction/
        │       └── FireCrawlApp.cs
        ├── Interfaces/
        │   ├── IAudioOutputModule.cs
        │   ├── ICommand.cs
        │   ├── IDisplayModule.cs
        │   ├── IJarvis.cs
        │   ├── IJarvisConfigManager.cs
        │   ├── IJarvisLogger.cs
        │   ├── IJarvisModule.cs
        │   ├── IMemoryManager.cs
        │   ├── IModuleRegistry.cs
        │   ├── IStarkArsenal.cs
        │   └── IVoiceInputModule.cs
        └── Models/
            ├── ModelName.cs
            └── Models.cs
```

## Important Components

1. **iJarvis (Console Application)**
   - `Program.cs`: Entry point of the application
   - `Startup.cs`: Configures services and dependencies
   - `MemoryManager.cs`: Manages the AI's memory
   - `Logger.cs`: Handles logging for the application
   - `config/`: Contains configuration files for the AI

2. **Jarvis.Ai (Core Library)**
   - `IronManSuit.cs`: Main class that initializes and manages core components
   - `JarvisAgent.cs`: Handles communication with OpenAI's API and manages conversation flow
   - `LLMClient.cs`: Client for interacting with the language model
   - `StarkFleetAssembler.cs`: Assembles and manages the AI's components

3. **Features**
   - `AudioProcessing/`: Handles voice input and output
   - `DiagramGeneration/`: Generates diagrams based on prompts
   - `StarkArsenal/`: Contains the core modules of the AI assistant
   - `VisualOutput/`: Manages the visual interface
   - `WebDataExtraction/`: Handles web scraping functionality

4. **Interfaces**
   - Define contracts for various components of the system, ensuring modularity and extensibility

5. **Models**
   - Contains data models used throughout the application

## Key Points

- The project uses a modular architecture, with each feature encapsulated in its own module.
- The `StarkArsenal` contains the core functionality of the AI assistant, with each module representing a specific capability.
- The project uses dependency injection (configured in `Startup.cs`) to manage dependencies and promote loose coupling.
- Configuration is managed through `appsettings.json` and `appsettings.local.json` files.
- The project includes both audio (voice) and visual interfaces for interaction.

This structure allows for easy expansion of the AI's capabilities by adding new modules to the `StarkArsenal` or new features to the `Features` directory.
