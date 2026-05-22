using MeinKraft.Worker;

public interface IGameSessionManager
{
    Task<Guid> StartSessionAsync(string worldName);
    Task StopSessionAsync(Guid sessionId);
    IReadOnlyDictionary<Guid, GameSession> ActiveSessions { get; }
}

public sealed class GameSession
{
    public Guid Id { get; init; }
    public string WorldName { get; init; }
    public int Port { get; init; }
    public IServerWorkerHost WorkerHost { get; init; }
}