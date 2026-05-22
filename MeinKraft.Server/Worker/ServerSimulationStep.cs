namespace MeinKraft.Worker;

public sealed class ServerSimulationStep : ISimulationStep
{
    private readonly IServer _server;
    private readonly ServerLifetime _lifetime;
    private bool _isConfigLoaded;

    public ServerSimulationStep(
        IServer server,
        ServerLifetime lifetime,
        ServerSystemBootstraper serverSystemBootstraper)
    {
        _server = server;
        _lifetime = lifetime;
        _server.Systems = serverSystemBootstraper.Systems;
    }

    public void Tick(float dt)
    { 
        if (!_isConfigLoaded)
        {
            _isConfigLoaded = true;
            _server.OnConfigLoaded();
        }

        _server.Process(dt);
    }
}