namespace MeinKraft;

/// <summary>
/// The last <see cref="ServerSystem"/> to run on startup. Use <see cref="Initialize"/>
/// to register anything that must execute after all other systems have initialized.
/// </summary>
public class ServerSystemLoadLast : ServerSystem
{
    private readonly ISaveGameService saveGameService;
    private readonly ServerGameService server;
    public ServerSystemLoadLast(ServerGameService server, IModEvents modEvents, ISaveGameService saveGameService) : base(modEvents)
    {
        this.server = server;
        this.saveGameService = saveGameService;
    }

    /// <inheritdoc/>
    protected override void Initialize() => CallModOnLoad();

    /// <summary>
    /// Fires all mod load callbacks in registration order.
    /// <para>
    /// <see cref="ServerGameService.OnLoad"/> handlers are called directly — errors there are
    /// considered fatal. <c>onloadworld</c> handlers are wrapped individually so a
    /// single bad mod cannot abort the rest of the load sequence.
    /// </para>
    /// </summary>
    private void CallModOnLoad()
    {
        // Ensure mod data storage exists even if nothing was loaded from a savegame
        saveGameService.ModData ??= [];

        foreach (Action handler in server.OnLoad)
        {
            handler();
        }

        ModEvents.RaiseLoadWorld();
    }
}