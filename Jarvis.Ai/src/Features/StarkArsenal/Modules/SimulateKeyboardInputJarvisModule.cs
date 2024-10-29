using System.Runtime.InteropServices;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Simulates keyboard input to type the specified text.")]
public class SimulateKeyboardInputJarvisModule : BaseJarvisModule
{
    [TacticalComponent("The text to type using simulated keyboard input.", "string", true)]
    public string Text { get; set; }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);

    private const int KEYEVENTF_EXTENDEDKEY = 0x1;
    private const int KEYEVENTF_KEYUP = 0x2;

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Add a small delay to allow user to focus the target window
            await Task.Delay(2000, cancellationToken);

            foreach (char c in Text)
            {
                cancellationToken.ThrowIfCancellationRequested();

                short vkKeyScan = VkKeyScan(c);
                byte virtualKeyCode = (byte)(vkKeyScan & 0xff);
                bool shiftKey = (vkKeyScan & 0x100) != 0;

                if (shiftKey)
                {
                    // Press Shift
                    keybd_event(0x10, 0, KEYEVENTF_EXTENDEDKEY, 0);
                }

                // Press and release the key
                keybd_event(virtualKeyCode, 0, KEYEVENTF_EXTENDEDKEY, 0);
                keybd_event(virtualKeyCode, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);

                if (shiftKey)
                {
                    // Release Shift
                    keybd_event(0x10, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
                }

                // Add a small delay between keystrokes
                await Task.Delay(50, cancellationToken);
            }

            return new Dictionary<string, object>
            {
                { "status", "success" },
                { "message", $"Successfully typed {Text.Length} characters" }
            };
        }
        catch (OperationCanceledException)
        {
            return new Dictionary<string, object>
            {
                { "status", "cancelled" },
                { "message", "Operation was cancelled" }
            };
        }
        catch (Exception e)
        {
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "message", $"Failed to simulate keyboard input: {e.Message}" }
            };
        }
    }
}
