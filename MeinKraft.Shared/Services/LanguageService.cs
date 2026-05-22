using System.Globalization;

/// <summary>
/// Manages localised string lookup for all game text. Strings are keyed by a
/// language code and a string ID; the active language is either the system locale
/// or an explicit <see cref="OverrideLanguage"/>. Falls back to English when a
/// string is missing in the active language.
/// </summary>
public class LanguageService : ILanguageService
{
    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    /// <summary>
    /// When set, overrides the system locale for all lookups.
    /// Set to <c>null</c> to restore automatic locale detection.
    /// </summary>
    public string? OverrideLanguage { get; set; }

    /// <summary>
    /// All loaded translation strings, keyed by <c>(language code, string ID)</c>.
    /// </summary>
    private readonly Dictionary<(string Language, string Id), string> strings = [];

    /// <summary>All distinct language codes seen so far, in insertion order.</summary>
    private readonly HashSet<string> loadedLanguages = [];

    // -------------------------------------------------------------------------
    // -------------------------------------------------------------------------
    // Loading
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans every <c>.json</c> file in the localisation directory and registers
    /// its entries. The filename without extension is used as the language code
    /// (e.g. <c>en.json</c> → <c>"en"</c>, <c>de.json</c> → <c>"de"</c>).
    /// Each file must be a flat JSON object mapping string IDs to translated values.
    /// </summary>
    public void LoadTranslations()
    {
        if (strings.Count > 0)
        {
            // Already loaded — don't load again
            return;
        }

        string[] fileList = FileHelper.DirectoryGetFiles(Path.Combine("data", "localization"));

        foreach (string file in fileList)
        {
            if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string language = Path.GetFileNameWithoutExtension(file);
            string json = File.ReadAllText(file, System.Text.Encoding.UTF8);

            Dictionary<string, string>? entries =
                System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (entries == null)
            {
                continue;
            }

            foreach ((string id, string translated) in entries)
            {
                Add(language, id, translated);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Lookup
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the translated string for <paramref name="id"/> in the active
    /// language, falling back to English if not found, or returning
    /// <paramref name="id"/> itself as a last resort.
    /// </summary>
    /// <param name="id">The translation key.</param>
    public string Get(string id)
    {
        string language = ResolveLanguage();

        if (strings.TryGetValue((language, id), out string result))
        {
            return result;
        }

        // Fallback to English
        if (strings.TryGetValue(("en", id), out string english))
        {
            return english;
        }

        // Not found — return the key so missing strings are visible in-game
        return id;
    }

    /// <summary>
    /// Returns the language code that will be used for lookups: the
    /// <see cref="OverrideLanguage"/> if set, otherwise the two-letter ISO code
    /// of the current system culture.
    /// </summary>
    public string GetUsedLanguage() => ResolveLanguage();

    /// <summary>Resolves the active language code.</summary>
    private string ResolveLanguage()
        => OverrideLanguage ?? CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

    // -------------------------------------------------------------------------
    // Language cycling
    // -------------------------------------------------------------------------

    /// <summary>
    /// Advances <see cref="OverrideLanguage"/> to the next loaded language,
    /// wrapping around to the first after the last. Used by the options screen
    /// language toggle.
    /// </summary>
    public void NextLanguage()
    {
        OverrideLanguage ??= "en";

        List<string> languages = [.. loadedLanguages];
        int index = languages.IndexOf(OverrideLanguage);

        index = (index + 1) % languages.Count;
        OverrideLanguage = languages[index];
    }

    // -------------------------------------------------------------------------
    // Mutation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds a translation only if no entry for <c>(language, id)</c> already
    /// exists. Use <see cref="Override"/> to replace an existing entry.
    /// </summary>
    /// <param name="language">Two-letter ISO language code (e.g. <c>"en"</c>).</param>
    /// <param name="id">Translation key.</param>
    /// <param name="translated">Localised string value.</param>
    private void Add(string language, string id, string translated)
    {
        _ = loadedLanguages.Add(language);
        _ = strings.TryAdd((language, id), translated);
    }

    /// <summary>
    /// Replaces an existing translation or adds it if not yet present.
    /// Use this to allow mods or server configs to patch individual strings.
    /// </summary>
    /// <param name="language">Two-letter ISO language code.</param>
    /// <param name="id">Translation key.</param>
    /// <param name="translated">Replacement localised string.</param>
    public void Override(string language, string id, string translated)
    {
        _ = loadedLanguages.Add(language);
        strings[(language, id)] = translated;
    }

    // -------------------------------------------------------------------------
    // Introspection
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a read-only view of all loaded translations, keyed by
    /// <c>(language code, string ID)</c>.
    /// </summary>
    public IReadOnlyDictionary<(string Language, string Id), string> AllStrings() => strings;

    // -------------------------------------------------------------------------
    // Typed convenience accessors
    // -------------------------------------------------------------------------

    public string CannotWriteChatLog() => Get("CannotWriteChatLog");
    public string ChunkUpdates() => Get("ChunkUpdates");
    public string Connecting() => Get("Connecting");
    public string ConnectingProgressKilobytes() => Get("ConnectingProgressKilobytes");
    public string ConnectingProgressPercent() => Get("ConnectingProgressPercent");
    public string DefaultKeys() => Get("DefaultKeys");
    public string Exit() => Get("Exit");
    public string FogDistance() => Get("FogDistance");
    public string FontOption() => Get("FontOption");
    public string FrameRateLagSimulation() => Get("FrameRateLagSimulation");
    public string FrameRateUnlimited() => Get("FrameRateUnlimited");
    public string FrameRateVsync() => Get("FrameRateVsync");
    public string FreemoveNotAllowed() => Get("FreemoveNotAllowed");
    public string GameName() => Get("GameName");
    public string Graphics() => Get("Graphics");
    public string InvalidVersionConnectAnyway() => Get("InvalidVersionConnectAnyway");
    public string KeyBlockInfo() => Get("KeyBlockInfo");
    public string KeyChange() => Get("KeyChange");
    public string KeyChat() => Get("KeyChat");
    public string KeyCraft() => Get("KeyCraft");
    public string KeyFreeMove() => Get("KeyFreeMove");
    public string KeyFullscreen() => Get("KeyFullscreen");
    public string KeyJump() => Get("KeyJump");
    public string KeyMoveBack() => Get("KeyMoveBack");
    public string KeyMoveFoward() => Get("KeyMoveFoward");
    public string KeyMoveLeft() => Get("KeyMoveLeft");
    public string KeyMoveRight() => Get("KeyMoveRight");
    public string KeyMoveSpeed() => Get("KeyMoveSpeed");
    public string KeyPlayersList() => Get("KeyPlayersList");
    public string KeyReloadWeapon() => Get("KeyReloadWeapon");
    public string KeyRespawn() => Get("KeyRespawn");
    public string KeyReverseMinecart() => Get("KeyReverseMinecart");
    public string Keys() => Get("Keys");
    public string KeyScreenshot() => Get("KeyScreenshot");
    public string KeySetSpawnPosition() => Get("KeySetSpawnPosition");
    public string KeyShowMaterialSelector() => Get("KeyShowMaterialSelector");
    public string KeyTeamChat() => Get("KeyTeamChat");
    public string KeyTextEditor() => Get("KeyTextEditor");
    public string KeyThirdPersonCamera() => Get("KeyThirdPersonCamera");
    public string KeyToggleFogDistance() => Get("KeyToggleFogDistance");
    public string KeyUse() => Get("KeyUse");
    public string MoveFree() => Get("MoveFree");
    public string MoveFreeNoclip() => Get("MoveFreeNoclip");
    public string MoveNormal() => Get("MoveNormal");
    public string MoveSpeed() => Get("MoveSpeed");
    public string NoMaterialsForCrafting() => Get("NoMaterialsForCrafting");
    public string Off() => Get("Off");
    public string On() => Get("On");
    public string Options() => Get("Options");
    public string Other() => Get("Other");
    public string PressToUse() => Get("PressToUse");
    public string Respawn() => Get("Respawn");
    public string ReturnToGame() => Get("ReturnToGame");
    public string ReturnToMainMenu() => Get("ReturnToMainMenu");
    public string ReturnToOptionsMenu() => Get("ReturnToOptionsMenu");
    public string ShadowsOption() => Get("ShadowsOption");
    public string SoundOption() => Get("SoundOption");
    public string AutoJumpOption() => Get("AutoJumpOption");
    public string ClientLanguageOption() => Get("ClientLanguageOption");
    public string SpawnPositionSet() => Get("SpawnPositionSet");
    public string SpawnPositionSetTo() => Get("SpawnPositionSetTo");
    public string Triangles() => Get("Triangles");
    public string UseServerTexturesOption() => Get("UseServerTexturesOption");
    public string ViewDistanceOption() => Get("ViewDistanceOption");
    public string OptionSmoothShadows() => Get("OptionSmoothShadows");
    public string OptionFramerate() => Get("OptionFramerate");
    public string OptionResolution() => Get("OptionResolution");
    public string OptionFullscreen() => Get("OptionFullscreen");

    public string ServerCannotWriteLog() => Get("Server_CannotWriteLogFile");
    public string ServerLoadingSavegame() => Get("Server_LoadingSavegame");
    public string ServerCreatingSavegame() => Get("Server_CreatingSavegame");
    public string ServerLoadedSavegame() => Get("Server_LoadedSavegame");
    public string ServerConfigNotFound() => Get("Server_ConfigNotFound");
    public string ServerConfigCorruptBackup() => Get("Server_ConfigCorruptBackup");
    public string ServerConfigCorruptNoBackup() => Get("Server_ConfigCorruptNoBackup");
    public string ServerConfigLoaded() => Get("Server_ConfigLoaded");
    public string ServerClientConfigNotFound() => Get("Server_ClientConfigNotFound");
    public string ServerClientConfigGuestGroupNotFound() => Get("Server_ClientConfigGuestGroupNotFound");
    public string ServerClientConfigRegisteredGroupNotFound() => Get("Server_ClientConfigRegisteredGroupNotFound");
    public string ServerClientConfigLoaded() => Get("Server_ClientConfigLoaded");
    public string ServerInvalidSpawnCoordinates() => Get("Server_InvalidSpawnCoordinates");
    public string ServerProgressDownloadingData() => Get("Server_ProgressDownloadingData");
    public string ServerProgressDownloadingMap() => Get("Server_ProgressDownloadingMap");
    public string ServerProgressGenerating() => Get("Server_ProgressGenerating");
    public string ServerNoChatPrivilege() => Get("Server_NoChatPrivilege");
    public string ServerFillAreaInvalid() => Get("Server_FillAreaInvalid");
    public string ServerFillAreaTooLarge() => Get("Server_FillAreaTooLarge");
    public string ServerNoSpectatorBuild() => Get("Server_NoSpectatorBuild");
    public string ServerNoBuildPrivilege() => Get("Server_NoBuildPrivilege");
    public string ServerNoBuildPermissionHere() => Get("Server_NoBuildPermissionHere");
    public string ServerNoSpectatorUse() => Get("Server_NoSpectatorUse");
    public string ServerNoUsePrivilege() => Get("Server_NoUsePrivilege");
    public string ServerPlayerJoin() => Get("Server_PlayerJoin");
    public string ServerPlayerDisconnect() => Get("Server_PlayerDisconnect");
    public string ServerUsernameBanned() => Get("Server_UsernameBanned");
    public string ServerNoGuests() => Get("Server_NoGuests");
    public string ServerUsernameInvalid() => Get("Server_UsernameInvalid");
    public string ServerPasswordInvalid() => Get("Server_PasswordInvalid");
    public string ServerClientException() => Get("Server_ClientException");
    public string ServerIPBanned() => Get("Server_IPBanned");
    public string ServerTooManyPlayers() => Get("Server_TooManyPlayers");
    public string ServerHTTPServerError() => Get("Server_HTTPServerError");
    public string ServerHTTPServerStarted() => Get("Server_HTTPServerStarted");
    public string ServerHeartbeatSent() => Get("Server_HeartbeatSent");
    public string ServerHeartbeatError() => Get("Server_HeartbeatError");
    public string ServerBanlistLoaded() => Get("Server_BanlistLoaded");
    public string ServerBanlistCorruptNoBackup() => Get("Server_BanlistCorruptNoBackup");
    public string ServerBanlistCorrupt() => Get("Server_BanlistCorrupt");
    public string ServerBanlistNotFound() => Get("Server_BanlistNotFound");
    public string ServerSetupAccept() => Get("Server_SetupAccept");
    public string ServerSetupEnableHTTP() => Get("Server_SetupEnableHTTP");
    public string ServerSetupMaxClients() => Get("Server_SetupMaxClients");
    public string ServerSetupMaxClientsInvalidValue() => Get("Server_SetupMaxClientsInvalidValue");
    public string ServerSetupMaxClientsInvalidInput() => Get("Server_SetupMaxClientsInvalidInput");
    public string ServerSetupPort() => Get("Server_SetupPort");
    public string ServerSetupPortInvalidValue() => Get("Server_SetupPortInvalidValue");
    public string ServerSetupPortInvalidInput() => Get("Server_SetupPortInvalidInput");
    public string ServerSetupWelcomeMessage() => Get("Server_SetupWelcomeMessage");
    public string ServerSetupMOTD() => Get("Server_SetupMOTD");
    public string ServerSetupName() => Get("Server_SetupName");
    public string ServerSetupPublic() => Get("Server_SetupPublic");
    public string ServerSetupQuestion() => Get("Server_SetupQuestion");
    public string ServerSetupFirstStart() => Get("Server_SetupFirstStart");
    public string ServerGameSaved() => Get("Server_GameSaved");
    public string ServerInvalidBackupName() => Get("Server_InvalidBackupName");
    public string ServerMonitorConfigLoaded() => Get("Server_MonitorConfigLoaded");
    public string ServerMonitorConfigNotFound() => Get("Server_MonitorConfigNotFound");
    public string ServerMonitorChatMuted() => Get("Server_MonitorChatMuted");
    public string ServerMonitorChatNotSent() => Get("Server_MonitorChatNotSent");
    public string ServerMonitorBuildingDisabled() => Get("Server_MonitorBuildingDisabled");

    // -------------------------------------------------------------------------
    // English defaults
}