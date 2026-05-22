using Microsoft.Extensions.Options;

namespace MeinKraft.Worker;

public sealed class ServerAutoRestartTask : IScheduledTask
{
    public TimeSpan Interval => TimeSpan.FromMinutes(10); // check every 10 min, no need to check every second

    private readonly IServer _server;
    private readonly ServerConfig _config;
    private readonly DateTimeOffset _startedAt;

    public ServerAutoRestartTask(IServer server, IOptions<ServerConfig> options)
    {
        _server = server;
        _config = options.Value;
        _startedAt = DateTimeOffset.UtcNow;
    }

    public Task ExecuteAsync(CancellationToken ct)
    {
        if (_config.AutoRestartCycle > 0 &&
            (DateTimeOffset.UtcNow - _startedAt).TotalHours >= _config.AutoRestartCycle)
        {
            _server.Restart();
        }

        return Task.CompletedTask;
    }
}