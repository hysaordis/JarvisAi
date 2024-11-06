using Microsoft.Extensions.Hosting.WindowsServices;

namespace Jarvis.Service;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Imposta Development come ambiente predefinito se non è già stato impostato
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")))
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        // Se stiamo eseguendo come servizio Windows, forza l'ambiente a "Production"
        if (WindowsServiceHelpers.IsWindowsService())
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
            Environment.SetEnvironmentVariable("DOTNET_RUNNING_AS_SERVICE", "true");
        }

        var host = CreateHostBuilder(args).Build();
        await host.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "iJarvis";
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
    }
}