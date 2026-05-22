using MeinKraft;
using System.Diagnostics;

/// <summary>
/// Sends a ping packet to every connected client once per second and disconnects
/// any client that fails to respond within the timeout window.
/// Half-dropped connections (where the TCP socket appears open but the client is
/// unresponsive) are detected this way.
/// </summary>
public class ServerSystemNotifyPing : ServerSystem
{
    private readonly ServerTimer pingTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1),
        MaxDeltaTime = TimeSpan.FromSeconds(5),
    };

    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private readonly IClientRegistry _serverClientService;
    private readonly IServerPacketService _serverPacketService;

    private int TimeMs => (int)_uptime.ElapsedMilliseconds;


    public ServerSystemNotifyPing(IModEvents modEvents, IClientRegistry serverClientService, 
        IServerPacketService serverPacketService) : base(modEvents)
    {
        _serverClientService = serverClientService;
        _serverPacketService = serverPacketService;
    }

    /// <inheritdoc/>
    protected override void OnUpdate(ServerGameService server, float dt)
    {
        pingTimer.Update(() =>
        {
            List<int> timedOut = [];

            foreach ((int clientId, ServerPlayer? client) in _serverClientService.Clients)
            {
                if (!client.Ping.Send(TimeMs))
                {
                    if (client.Ping.CheckTimeout(TimeMs))
                    {
                        Console.WriteLine($"{clientId}: ping timeout. Disconnecting...");
                        timedOut.Add(clientId);
                    }
                }
                else
                {
                    _serverPacketService.SendPacket(clientId, ServerPackets.Ping());
                }
            }

            foreach (int clientId in timedOut)
            {
                server.KillPlayer(clientId);
            }
        });
    }
}