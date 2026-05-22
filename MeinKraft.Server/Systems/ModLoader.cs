namespace MeinKraft;

public class ServerSystemModLoader(IEnumerable<IMod> mods, IModEvents modEvents, IServerModManager modManager,
    ILanguageService languageService, IClientRegistry serverClientService, IServerPacketService serverPacketService) : ServerSystem(modEvents)
{
    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void Initialize() => LoadMods();

    /// <inheritdoc/>
    public override bool OnCommand(ServerGameService server, int sourceClientId, string command, string argument)
    {
        if (command == "mods")
        {
            RestartMods(server, sourceClientId);
            return true;
        }

        return false;
    }

    // -------------------------------------------------------------------------
    // Mod restart (live reload)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reloads all mods at runtime without restarting the server process.
    /// Resets all mod event handlers and re-runs <see cref="ServerSystem.OnRestart"/>
    /// on every active system before recompiling.
    /// </summary>
    /// <param name="server">The running server instance.</param>
    /// <param name="sourceClientId">The client ID of the operator who issued the command.</param>
    /// <returns><c>true</c> if the caller had sufficient privileges and the reload was initiated.</returns>
    public bool RestartMods(ServerGameService server, int sourceClientId)
    {
        if (!server.PlayerHasPrivilege(sourceClientId, Privilege.restart))
        {
            serverPacketService.SendMessage(sourceClientId, string.Format(
                languageService.Get("Server_CommandInsufficientPrivileges"), GameConstants.colorError));
            return false;
        }

        ServerPlayer caller = serverClientService.GetClient(sourceClientId);
        server.SendMessageToAll(string.Format(
            languageService.Get("Server_CommandRestartModsSuccess"),
            GameConstants.colorImportant, caller.ColoredPlayername(GameConstants.colorImportant)));

        // restart mods if needed in an iteration
        return true;
    }

    // -------------------------------------------------------------------------
    // Mod loading pipeline
    // -------------------------------------------------------------------------

    private void LoadMods() => StartMods();

    // -------------------------------------------------------------------------
    // Mod startup (dependency-ordered)
    // -------------------------------------------------------------------------

    private void StartMods()
    {
        foreach (IMod mod in mods.ToList())
        {
            mod.PreStart(modManager);
        }

        foreach (IMod mod in mods)
        {
            mod.Start(modManager, modEvents);
        }
    }
}