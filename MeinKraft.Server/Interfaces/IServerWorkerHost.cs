namespace MeinKraft.Worker;

public interface IServerWorkerHost
{
    Task StartAsync();
    Task StopAsync();
}