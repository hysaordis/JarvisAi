using Jarvis.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Jarvis.Console;

class Program
{
    static async Task Main(string[] args)
    {
        using IHost host = CreateHostBuilder(args).Build();

        var startup = new Startup();
        startup.Configure(host.Services);

        var ironManSuit = host.Services.GetRequiredService<IronManSuit>();

        System.Console.WriteLine("Initializing IronMan Suit...");

        string[] startupCommands = ParseArguments(args);

        try
        {
            await ironManSuit.ActivateAsync(startupCommands);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"An error occurred: {ex.Message}");
        }

        System.Console.WriteLine("IronMan Suit deactivated. Press any key to exit.");
        System.Console.ReadKey();

        await host.StopAsync();
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                var startup = new Startup();
                startup.ConfigureServices(services);
            });

    static string[] ParseArguments(string[] args)
    {
        if (args.Length > 1 && args[0] == "--prompts")
        {
            return args[1].Split('|', StringSplitOptions.RemoveEmptyEntries);
        }
        return args;
    }
}