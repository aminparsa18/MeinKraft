using MeinKraft;
using MeinKraft.Server;
using MeinKraft.Worker;

public static class WorkerServerExtensions
{
    /// <summary>
    /// Registers the simulation loop, periodic task scheduler, and WorkerHost as singletons.
    /// Usage:
    /// <code>
    /// services.AddWorkerInfrastructure()
    ///         .AddScheduledTask&lt;SaveGameTask&gt;()
    ///         .AddScheduledTask&lt;SeasonBroadcastTask&gt;();
    /// </code>
    /// </summary>
    public static IServiceCollection AddServerWorkerInfrastructure(
        this IServiceCollection services)
    {
        // Session manager and port allocator are singleton — they live for the 
        // lifetime of the web app and track all active sessions
        services.AddSingleton<IGameSessionManager, GameSessionManager>();
        services.AddSingleton<PortAllocator>();

        services.AddScoped<ISessionConfig, SessionConfig>();
        services.AddScoped<ServerWorkerHost>();
        services.AddScoped<SimulationLoop>();
        services.AddScoped<ServerLifetime>();
        services.AddScoped<ISimulationStep, ServerSimulationStep>();
        services.AddScoped<ServerGameService>();
        services.AddScoped<PeriodicTaskScheduler>();

        services.AddScheduledTask<ServerAutoRestartTask>();
        services.AddScheduledTask<SaveGameTask>();
        services.AddScheduledTask<SeasonBroadcastTask>();

        return services;
    }

    public static IServiceCollection AddScheduledTask<T>(this IServiceCollection services)
    where T : class, IScheduledTask
    {
        services.AddScoped<IScheduledTask, T>();
        return services;
    }
}