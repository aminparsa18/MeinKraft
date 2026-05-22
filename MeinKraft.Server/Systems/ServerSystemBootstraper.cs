using MeinKraft;

public class ServerSystemBootstraper
{
    public List<ServerSystem> Systems { get; }

    public ServerGameService Server { get; }

    public ServerSystemBootstraper(
        ServerGameService server,
        ServerSystemLoadFirst loadFirst,
        ServerSystemHeartbeat heartbeat,
        ServerSystemHttpServer httpServer,
        ServerSystemUnloadUnusedChunks unloadChunks,
        ServerSystemNotifyMap notifyMap,
        ServerSystemNotifyPing notifyPing,
        ServerSystemChunksSimulation chunksSimulation,
        ServerSystemBanList banList,
        ServerSystemModLoader modLoader,
        ServerSystemLoadServerClient loadServerClient,
        ServerSystemNotifyEntities notifyEntities,
        ServerSystemLoadLast loadLast)
    {
        Server = server;

        Systems =
        [
            loadFirst,
            heartbeat,
            httpServer,
            unloadChunks,
            notifyMap,
            notifyPing,
            chunksSimulation,
            banList,
            modLoader,
            loadServerClient,
            notifyEntities,
            loadLast,
        ];

        server.Systems = Systems;
    }
}