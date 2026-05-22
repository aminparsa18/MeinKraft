namespace MeinKraft.Extensions;

public static class BuilderServerServicesExtensions
{
    public static IServiceCollection AddServerServices(this IServiceCollection services,
                                                    IConfiguration configuration)
    {
        services.AddSingleton<IAssetManager, ServerAssetManager>();

        // ── Per-session — one instance per game world ─────────────────────────
        services.AddScoped<ServerGameService>();
        services.AddScoped<IServer>(sp => sp.GetRequiredService<ServerGameService>());
        services.AddScoped<GameTimer>();

        services.AddScoped<IServerMapStorage, ServerMapStorage>();
       
        services.AddScoped<IPlayerStatusService, PlayerStatusService>();
        services.AddScoped<IClientRegistry, ClientRegistry>();
        services.AddScoped<IServerPacketService, ServerPacketService>();
        services.AddScoped<ISaveGameService, SaveGameService>();

        services.AddScoped<IChunkDbCompressed, ChunkDbCompressed>();
        services.AddScoped<IChunkDbRegion, ChunkDbRegion>();

        services.AddScoped<ServerSystemLoadFirst>();
        services.AddScoped<ServerSystemHeartbeat>();
        services.AddScoped<ServerSystemHttpServer>();
        services.AddScoped<ServerSystemUnloadUnusedChunks>();
        services.AddScoped<ServerSystemNotifyMap>();
        services.AddScoped<ServerSystemNotifyPing>();
        services.AddScoped<ServerSystemChunksSimulation>();
        services.AddScoped<ServerSystemBanList>();
        services.AddScoped<ServerSystemModLoader>();
        services.AddScoped<ServerSystemLoadServerClient>();
        services.AddScoped<ServerSystemNotifyEntities>();
        services.AddScoped<ServerSystemLoadLast>();

        services.AddScoped<ServerSystemBootstraper>();

        services.Configure<ServerConfig>(configuration.GetSection("ServerConfig"));

        return services;
    }
}
