using MeinKraft.Worker;
using System.Collections.Concurrent;

namespace MeinKraft.Server;

public sealed class GameSessionManager : IGameSessionManager
{
    private readonly ConcurrentDictionary<Guid, GameSession> _sessions = new();
    private readonly IServiceProvider _services;
    private readonly PortAllocator _ports;

    public IReadOnlyDictionary<Guid, GameSession> ActiveSessions => _sessions;

    public GameSessionManager(IServiceProvider services, PortAllocator ports)
    {
        _services = services;
        _ports = ports;
    }

    public async Task<Guid> StartSessionAsync(string worldName)
    {
        // Each session gets its own DI scope — isolated SimulationLoop,
        // ServerGameService, ENet socket, everything.
        var scope = _services.CreateScope();
        var workerHost = scope.ServiceProvider.GetRequiredService<ServerWorkerHost>();
        int port = _ports.Allocate();

        // Tell this session's server which port and world to use
        var config = scope.ServiceProvider.GetRequiredService<ISessionConfig>();
        config.WorldName = worldName;
        config.Port = port;

        await workerHost.StartAsync();

        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            WorldName = worldName,
            Port = port,
            WorkerHost = workerHost,
        };

        _sessions[session.Id] = session;
        return session.Id;
    }

    public async Task StopSessionAsync(Guid sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            await session.WorkerHost.StopAsync();
            _ports.Release(session.Port);
        }
    }
}

public sealed class PortAllocator(int start = 7777, int count = 100)
{
    private readonly ConcurrentQueue<int> _available = new(Enumerable.Range(start, count));

    public int Allocate() => _available.TryDequeue(out int port) 
        ? port : throw new InvalidOperationException("No ports available");

    public void Release(int port) => _available.Enqueue(port);
}