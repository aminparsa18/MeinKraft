using MeinKraft;

public abstract class ServerSystem
{
    private bool _initialized;

    protected readonly IModEvents ModEvents;

    public ServerSystem(IModEvents modEvents)
    {
        ModEvents = modEvents;
    }

    public void Update(ServerGameService server, float dt)
    {
        if (!_initialized)
        {
            _initialized = true;
            Initialize();
        }

        OnUpdate(server, dt);
    }

    protected virtual void Initialize() { }
    protected virtual void OnUpdate(ServerGameService server, float dt) { }
    public virtual void OnRestart(ServerGameService server) { }
    public virtual bool OnCommand(ServerGameService server, int sourceClientId, string command, string argument) => false;
}