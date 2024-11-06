![Jarvis AI](image.webp)

# Jarvis.AI

Jarvis.AI is an AI assistant inspired by the AI from the Iron Man series. Built using C#, it leverages multiple AI technologies to offer a multifunctional assistant capable of various tasks. The project supports both high-performance cloud services and cost-effective local alternatives, allowing users to balance performance and cost according to their needs.

## ⚡ New Features

- **Client Console Transformation**: The client console has been transformed into a system service, hosting a Kestrel web server with SignalR support for real-time communication.
- **Agent Control via SignalR**:
  - Start the agent in Listening Mode.
  - Stop the agent and put it in Idle Mode.
- **Event Broadcasting**: The SignalR hub broadcasts various events to connected clients, as listed in the `SignalRService.js` file.
- **Chat Interface**:
  - Modern, responsive chat UI with real-time message updates
  - Keyboard shortcuts (ESC to close)
  - Message history with timestamps
  - Smooth scroll and animations
  - Backdrop blur effects for better readability

## 🛣️ Roadmap

Here are some of the planned features and improvements for Jarvis.AI:

- **Desktop Client Improvements**: Continue refining the desktop client built with React and Tauri to ensure a user-friendly interface with essential command controls.
- Text-in / Text-out Communication (inspired by [joelmnz](https://github.com/joelmnz))
- New Console Client
- Expanded Tool Ecosystem
- Performance Optimization
- Enhanced Security and Privacy
- Integration with External Services
- Improved Documentation and Tutorials

> **Note**: This roadmap is subject to change based on user feedback, priorities, and resources. We welcome suggestions and contributions from the community to shape the future of Jarvis.AI.
>

## 🌟 Features

- **Voice Interaction**: Voice input and output for natural communication.

  - Multiple transcription options:
    - **WhisperTranscriber**: Local speech-to-text using Whisper for cost-effective processing
    - **AssemblyAITranscriber**: Cloud-based alternative with high accuracy
- **Visual Interface**: Graphical user interface for enhanced interaction and data visualization.
- **Flexible LLM Integration**:

  - **OpenAI**: High performance with cloud-based processing
  - **Ollama**: Local LLM support for cost-effective operation with acceptable latency
- **WebSocket Communication**: Real-time communication with AI services.
- **Modular Architecture**: Well-designed architecture allows easy extension of Jarvis modules.
- **Persistent Memory**: Stores context for personalized interactions.
- **Tool Integration**: Wide range of tools for various tasks.

## 🛠️ Technologies Used

- C# and .NET
- WebSocket (real-time communication)
- OpenAI API (cloud LLM)
- Ollama (local LLM)
- Whisper.net (local speech-to-text)
- AssemblyAI API (cloud speech-to-text)
- JSON (data serialization/deserialization)
- SignalR (real-time client-server communication)

## 💡 Key Components

- **JarvisAgent**: The most capable agent at the moment. Utilizes OpenAI's ChatGPT in real-time mode for advanced conversational abilities and manages the conversation flow.
- **AlitaAgent**: An orchestration of LLM services based on text-to-text, using various transcription and TTS techniques to simulate a bidirectional voice-to-voice conversation with the agent. The performance depends on the Transcribe and TTS services used, as well as the chosen LLM. Currently, this agent can integrate with various LLMs such as OpenAI and Ollama, with easy expandability to support more models in the future.
- **AlitaHub**: SignalR hub for real-time communication between the backend service and connected clients.
- **SignalRService**: JavaScript service for establishing and maintaining SignalR connection with the backend.
- **VoiceInterface**: React component that serves as the primary interface for interacting with the agents. It allows users to start and stop the agents and visualizes various agent states.
- **LogConsole**: A crucial React component for displaying real-time logs and events received via SignalR. When the agent is installed as a service, the console provides the ability to view various logs during the execution of the application. There are plans to extend the functionality of the LogConsole to allow sending text to the agent, enabling a more interactive experience.

## 🚀 Automating Deployment with BuildAndDeployService.ps1

The `BuildAndDeployService.ps1` script automates the process of building, publishing, and installing the iJarvis service. It provides a menu with options to build and run iJarvis and/or the visual interface in development mode, build and publish the service, install the service, and uninstall the service.

To use the script:

1. Open PowerShell as administrator.
2. Navigate to the script directory.
3. Run `.\BuildAndDeployService.ps1`.
4. Choose an option from the menu.

Ensure you have the .NET SDK installed and the script is in the project root directory. For a full deployment, use the "Build, Publish, and Install Service" option.

## 🗣️ Voice Commands Examples

- "Jarvis, what's the current time?"
- "Open ChatGPT, Claude, and Hacker News."
- "Generate a diagram outlining the architecture of a minimal TikTok clone."

## 🌈 Inspiration

This project draws inspiration from a Python-based AI assistant by [disler](https://github.com/disler/poc-realtime-ai-assistant). For more tutorials, check out [Indy Dev Dan&#39;s YouTube channel](https://www.youtube.com/@indydevdan).

## 🧰 Extending Tools

Jarvis.AI provides a modular architecture that allows for easy extension of its capabilities through the creation of new tools or modification of existing ones. To learn how to extend and create tools for Jarvis.AI, please refer to the [Extending Tools Guide](ExtendingTools.md).

## 📄 License

This project is licensed under the MIT License.

## 📞 Contact

For any inquiries or contributions, contact:

Ordis Hysa

- Email: [ordishysa@gmail.com](mailto:ordishysa@gmail.com)
- [LinkedIn](https://www.linkedin.com/in/ordishysa/)

---

Built with ❤️ by Ordis Hysa

---
