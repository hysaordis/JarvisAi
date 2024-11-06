using Jarvis.Ai;
using Jarvis.Ai.Features.AudioProcessing;
using Jarvis.Ai.Features.VisualOutput;
using Jarvis.Ai.Interfaces;
using Jarvis.Service.Config;
using Jarvis.Service.Hubs;
using Microsoft.AspNetCore.Http.Connections;
using System.Reflection;

namespace Jarvis.Service;

public class Startup
{
    public Startup()
    {

    }
    public void ConfigureServices(IServiceCollection services)
    {
        // Register configuration and logging services
        services.AddSingleton<IJarvisConfigManager, JarvisConfigManager>();
        services.AddSingleton<IJarvisLogger, Logger>();

        var _configuration = services.BuildServiceProvider().GetRequiredService<IJarvisConfigManager>();

        var transcriberType = _configuration.GetValue("TranscriberType") ?? "1";

        // Register core Jarvis systems
        services.AssembleJarvisSystems(_configuration);

        // Register input/output modules
        services.AddSingleton<IVoiceInputModule, VoiceInputModule>();
        services.AddSingleton<IAudioOutputModule, AudioOutputModule>();
        //services.AddSingleton<IAudioOutputModule, WindowsAudioOutputModule>();
        services.AddSingleton<IDisplayModule, VisualInterfaceModule>();

        // Register memory management
        services.AddSingleton<IMemoryManager, MemoryManager>();

        // Update transcriber registration
        switch (transcriberType)
        {
            case "3":
                services.AddSingleton<ITranscriber, AssemblyAIRealtimeTranscriber>();
                break;
            case "2":
                services.AddSingleton<ITranscriber, AssemblyAITranscriber>();
                break;
            case "1":
            default:
                services.AddSingleton<ITranscriber, WhisperTranscriber>();
                break;
        }

        // Register the main AI agent (only one should be active at a time)
        //services.AddSingleton<JarvisAgent>();
        services.AddSingleton<AlitaAgent>();

        // Register IronManSuit which depends on IJarvis
        //services.AddSingleton<IronManSuit>();

        // Register ASP.NET Core services
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // Add MVC Core services
        services.AddMvcCore();

        // Add SignalR with CORS first
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.MaximumReceiveMessageSize = 102400000;
        }).AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy = null;
        });

        // Then configure CORS
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.WithOrigins("http://localhost:1420", "tauri://localhost")
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .AllowCredentials()
                       .SetIsOriginAllowed(_ => true); // Be careful with this in production
            });
        });

        // Add required HTTP services
        services.AddHttpContextAccessor();
        services.AddRouting();

        // Add controllers if needed
        services.AddControllers();

        // Register your Hub
        services.AddSingleton<AlitaHub>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Enable WebSocket support
        app.UseWebSockets();

        // Abilita sempre Swagger in fase di test
        app.UseSwagger();
        app.UseSwaggerUI();

        // Configure the HTTP request pipeline
        app.UseRouting();

        // CORS must be between UseRouting and UseEndpoints
        app.UseCors();

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapHub<AlitaHub>("/alitahub", options =>
            {
                options.Transports =
                    HttpTransportType.WebSockets |
                    HttpTransportType.LongPolling;
                options.WebSockets.CloseTimeout = TimeSpan.FromSeconds(30);
            });

            // Map controllers if you have any
            endpoints.MapControllers();
        });

        var moduleRegistry = app.ApplicationServices.GetRequiredService<IModuleRegistry>();
        moduleRegistry.RegisterCommandsFromAssembly();
    }
}