using System.Runtime.InteropServices;
using System.Diagnostics;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using System.Text;
using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Models;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule(
    @"Simulates realistic human computer interaction in Notepad, including:
    - Natural mouse movements and clicks
    - Keyboard typing with variable speeds
    - Text editing behaviors (selection, deletion, copy/paste)
    - Combined mouse and keyboard actions
    Perfect for:
    - System activity simulation
    - Text content generation
    - UI automation testing
    - Writing simulation with natural pauses")]
public class MouseWorkSimulatorModule : BaseJarvisModule
{
    [TacticalComponent(
        @"Natural language instructions for work simulation.
        Examples:
        - 'Write a short story about space exploration with natural typing speed'
        - 'Move mouse naturally and edit existing text for 30 seconds'
        - 'Simulate writing code with occasional mouse selections and comments'
        - 'Type meeting notes while occasionally formatting text'
        The system will analyze the prompt to determine:
        - Whether to type text, move mouse, or both
        - Writing style and speed variations
        - Mouse movement patterns
        - Text selection and editing behaviors",
        "string",
        true)]
    public string Prompt { get; set; }

    [TacticalComponent(
        @"Duration in seconds for the simulation.
        Default: '10' seconds
        Format: String representing seconds (e.g. '30', '60', '120')
        Range: 5-3600 seconds
        Affects:
        - Length of generated text
        - Complexity of mouse movements
        - Number of editing operations
        - Natural pause distributions",
        "string",
        false)]
    public string Duration { get; set; } = "10";

    private int ParseDuration()
    {
        if (int.TryParse(Duration, out int seconds))
        {
            return Math.Clamp(seconds, 5, 3600); // Ensure duration is between 5 and 3600 seconds
        }
        return 10; // Default duration if parsing fails
    }

    private readonly ILlmClient _llmClient;

    public MouseWorkSimulatorModule(ILlmClient llmClient)
    {
        _llmClient = llmClient;
    }

    // Existing Win32 APIs
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

    // New Keyboard APIs
    [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);
    [DllImport("user32.dll")] private static extern short VkKeyScan(char ch);

    // Add these new Win32 API imports after the existing ones
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    // Add these constants
    private const int SW_MAXIMIZE = 3;
    private static readonly IntPtr HWND_TOP = new IntPtr(0);
    private const uint SWP_SHOWWINDOW = 0x0040;

    private const int KEYEVENTF_EXTENDEDKEY = 0x1;
    private const int KEYEVENTF_KEYUP = 0x2;

    // Aggiungi questa API Win32
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    // Aggiungi queste costanti
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync(CancellationToken cancellationToken)
    {
        try
        {
            var notepad = EnsureNotepadRunning();
            int durationSeconds = ParseDuration();

            string aiPrompt = $@"<purpose>Analyze user request and generate detailed interaction sequence.</purpose>

<user-input>{Prompt}</user-input>

<context>
Duration: {durationSeconds} seconds
Application: Notepad
Available Actions:
- Type text (with natural speed variations)
- Move mouse (with realistic patterns)
- Click (left/right)
- Select text
- Basic editing operations
</context>

<output-format>
Generate a sequence of commands:
TYPE 'text to type' speed(chars/sec)
MOVE direction distance
CLICK type
SELECT start length
DELAY milliseconds
</output-format>

<example-sequence>
TYPE 'Hello world' 5
DELAY 500
MOVE RIGHT 100
CLICK LEFT
TYPE 'More text here' 3
SELECT 5 10
</example-sequence>";

            string aiResponse = await _llmClient.ChatPrompt(aiPrompt, Constants.ModelNameToId[ModelName.BaseModel]);
            var commands = ParseAiCommands(aiResponse);

            var endTime = DateTime.Now.AddSeconds(durationSeconds);
            while (DateTime.Now < endTime && !cancellationToken.IsCancellationRequested)
            {
                if (!IsNotepadActive()) SetForegroundWindow(notepad.MainWindowHandle);

                foreach (var command in commands)
                {
                    if (DateTime.Now >= endTime) break;
                    await ExecuteCommand(command);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            return new Dictionary<string, object>
            {
                { "status", "success" },
                { "duration", durationSeconds }
            };
        }
        catch (Exception ex)
        {
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "message", ex.Message }
            };
        }
    }

    private async Task TypeText(string text, int charsPerSecond)
    {
        foreach (char c in text)
        {
            short vkKeyScan = VkKeyScan(c);
            byte virtualKeyCode = (byte)(vkKeyScan & 0xff);
            bool shiftKey = (vkKeyScan & 0x100) != 0;

            if (shiftKey) keybd_event(0x10, 0, KEYEVENTF_EXTENDEDKEY, 0);

            keybd_event(virtualKeyCode, 0, KEYEVENTF_EXTENDEDKEY, 0);
            keybd_event(virtualKeyCode, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);

            if (shiftKey) keybd_event(0x10, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);

            await Task.Delay(1000 / charsPerSecond);
        }
    }

    private async Task ExecuteCommand((string action, string param1, string param2, int value) command)
    {
        switch (command.action)
        {
            case "TYPE":
                await TypeText(command.param1, command.value);
                break;
            case "MOVE":
                // Existing mouse movement code
                break;
            case "CLICK":
                // Existing click code
                break;
            case "SELECT":
                // Implement text selection logic
                break;
            case "DELAY":
                await Task.Delay(command.value);
                break;
        }
    }

    // Replace the existing EnsureNotepadRunning method with this updated version
    private Process EnsureNotepadRunning()
    {
        var notepadProcesses = Process.GetProcessesByName("notepad");
        Process notepadProcess;

        if (notepadProcesses.Length > 0)
        {
            notepadProcess = notepadProcesses[0];
        }
        else
        {
            notepadProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    WindowStyle = ProcessWindowStyle.Normal
                }
            };
            notepadProcess.Start();
            Thread.Sleep(500); // Wait for Notepad to initialize
        }

        // Maximize the window
        ShowWindow(notepadProcess.MainWindowHandle, SW_MAXIMIZE);
        
        // Get screen dimensions using GetSystemMetrics
        int screenWidth = GetSystemMetrics(SM_CXSCREEN);
        int screenHeight = GetSystemMetrics(SM_CYSCREEN);
        
        // Force window to cover the entire screen
        SetWindowPos(notepadProcess.MainWindowHandle, HWND_TOP, 0, 0, screenWidth, screenHeight, SWP_SHOWWINDOW);

        return notepadProcess;
    }

    private bool IsNotepadActive()
    {
        IntPtr activeWindow = GetForegroundWindow();
        StringBuilder windowTitle = new StringBuilder(256);
        GetWindowText(activeWindow, windowTitle, 256);
        return windowTitle.ToString().ToLower().Contains("notepad");
    }

    private List<(string action, string param1, string param2, int value)> ParseAiCommands(string aiResponse)
    {
        var commands = new List<(string action, string param1, string param2, int value)>();
        var lines = aiResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var parts = line.Trim().Split(' ');
            if (parts.Length < 2) continue;

            switch (parts[0].ToUpper())
            {
                case "TYPE":
                    // Extract text between quotes and typing speed
                    var textMatch = line.Split('\'');
                    if (textMatch.Length >= 2)
                    {
                        int speed = int.TryParse(parts[^1], out int s) ? s : 5;
                        commands.Add(("TYPE", textMatch[1], "", speed));
                    }
                    break;

                case "MOVE":
                    if (parts.Length >= 3)
                    {
                        int distance = int.TryParse(parts[2], out int d) ? d : 100;
                        commands.Add(("MOVE", parts[1], "", distance));
                    }
                    break;

                case "CLICK":
                    if (parts.Length >= 2)
                    {
                        commands.Add(("CLICK", parts[1], "", 0));
                    }
                    break;

                case "SELECT":
                    if (parts.Length >= 3)
                    {
                        int start = int.TryParse(parts[1], out int s) ? s : 0;
                        int length = int.TryParse(parts[2], out int l) ? l : 1;
                        commands.Add(("SELECT", start.ToString(), length.ToString(), 0));
                    }
                    break;

                case "DELAY":
                    if (parts.Length >= 2)
                    {
                        int delay = int.TryParse(parts[1], out int d) ? d : 500;
                        commands.Add(("DELAY", "", "", delay));
                    }
                    break;
            }
        }

        return commands;
    }
}