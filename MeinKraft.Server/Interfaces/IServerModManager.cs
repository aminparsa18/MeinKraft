namespace MeinKraft;

public interface IServerModManager
{
    /// <summary>
    /// Get the maximum number of blocks supported by the server (default: 1024)
    /// </summary>
    /// <returns>Maximum possible BlockTypes</returns>
    int GetMaxBlockTypes();

    /// <summary>
    /// Set a new BlockType
    /// </summary>
    /// <param name="id">ID of the new BlockType (has to be unique)</param>
    /// <param name="name">Name of the new block</param>
    /// <param name="block">BlockType to register</param>
    void SetBlockType(int id, string name, BlockType block);

    /// <summary>
    /// Set a new BlockType and automatically assign the next free ID
    /// </summary>
    /// <param name="name">Name of the new block</param>
    /// <param name="block">BlockType to register</param>
    void SetBlockType(string name, BlockType block);

    /// <summary>
    /// Get the ID of a certain BlockType
    /// </summary>
    /// <param name="name">Name of the BlockType</param>
    /// <returns>ID of the BlockType</returns>
    int GetBlockId(string name);

    /// <summary>
    /// Add the given block to inventory in creative mode
    /// </summary>
    /// <param name="blockType">Name of the BlockType</param>
    void AddToCreativeInventory(string blockType);

    int GetMapSizeX();
    int GetMapSizeY();
    int GetMapSizeZ();

    /// <summary>
    /// Get ID of a certain block
    /// </summary>
    /// <param name="x">x coordinate</param>
    /// <param name="y">y coordinate</param>
    /// <param name="z">z coordinate</param>
    /// <returns>ID of the block at the given position</returns>
    int GetBlock(int x, int y, int z);

    /// <summary>
    /// Get the name of a BlockType
    /// </summary>
    /// <param name="blockType">ID of the BlockType</param>
    /// <returns>Name of the BlockType</returns>
    string GetBlockName(int blockType);

    /// <summary>
    /// Get the name of a certain block
    /// </summary>
    /// <param name="x">x coordinate</param>
    /// <param name="y">y coordinate</param>
    /// <param name="z">z coordinate</param>
    /// <returns>Name of the block at the given position</returns>
    string GetBlockNameAt(int x, int y, int z);

    /// <summary>
    /// Set a block at the given position
    /// </summary>
    /// <param name="x">x coordinate</param>
    /// <param name="y">y coordinate</param>
    /// <param name="z">z coordinate</param>
    /// <param name="tileType">The block to place</param>
    void SetBlock(int x, int y, int z, int tileType);

    void SetSunLevels(int[] sunLevels);
    void SetLightLevels(float[] lightLevels);
    void AddCraftingRecipe(string output, int outputAmount, string Input0, int Input0Amount);
    void AddCraftingRecipe2(string output, int outputAmount, string Input0, int Input0Amount, string Input1, int Input1Amount);
    void AddCraftingRecipe3(string output, int outputAmount, string Input0, int Input0Amount, string Input1, int Input1Amount, string Input2, int Input2Amount);

    /// <summary>
    /// Sets the given string as translation
    /// </summary>
    /// <param name="language">Language code of the translated stirng</param>
    /// <param name="id">ID string for the translation (should be unique)</param>
    /// <param name="translation">The translation</param>
    void SetString(string language, string id, string translation);

    /// <summary>
    /// Checks if a given position is valid
    /// </summary>
    /// <param name="x">x coordinate</param>
    /// <param name="y">y coordinate</param>
    /// <param name="z">z coordinate</param>
    /// <returns>true if position is inside map bounds</returns>
    bool IsValidPos(int x, int y, int z);

    void RegisterTimer(Action a, double interval);

    /// <summary>
    /// Plays a sound at the given position. Every player on the server will hear this sound. Only sounds that were present when the user joined will be played.
    /// </summary>
    /// <param name="x">x coordinate</param>
    /// <param name="y">y coordinate</param>
    /// <param name="z">z coordinate</param>
    /// <param name="sound">Filename of the sound to play</param>
    void PlaySoundAt(int x, int y, int z, string sound);

    /// <summary>
    /// Find the nearest player to the given position
    /// </summary>
    /// <param name="x">x coordinate</param>
    /// <param name="y">y coordinate</param>
    /// <param name="z">z coordinate</param>
    /// <returns>ID of the nearest player</returns>
    int NearestPlayer(int x, int y, int z);

    /// <summary>
    /// Give one block to the player
    /// </summary>
    /// <param name="player"></param>
    /// <param name="block">ID of the block to give</param>
    void GrabBlock(int player, int block);

    /// <summary>
    /// Give a certain amount of blocks to the player
    /// </summary>
    /// <param name="player"></param>
    /// <param name="block">ID of the block to give</param>
    /// <param name="amount">Amount to give</param>
    void GrabBlocks(int player, int block, int amount);

    /// <summary>
    /// Check if a player has the given privilege
    /// </summary>
    /// <param name="player"></param>
    /// <param name="p">The privilege to check</param>
    /// <returns>true if the player has the given privilege, false otherwise</returns>
    bool PlayerHasPrivilege(int player, string p);

    bool IsCreative { get; }

    bool IsBlockFluid(int block);

    /// <summary>
    /// Mark the player's inventory as "dirty" so it is resent
    /// </summary>
    /// <param name="player"></param>
    void NotifyInventory(int player);

    /// <summary>
    /// Sends a message to the given player. No formatting is done. Message is sent as given
    /// </summary>
    /// <param name="player"></param>
    /// <param name="p">Message to send</param>
    void SendMessage(int player, string p);

    /// <summary>
    /// Registers the given privilege with the server. This allows server console to have that privilege by default
    /// </summary>
    /// <param name="p">Privilege to register</param>
    void RegisterPrivilege(string p);

    int GetChunkSize();

    /// <summary>
    /// Get the seed used to generate the current world
    /// </summary>
    /// <returns>The map seed</returns>
    int Seed { get; }

    /// <summary>
    /// Sets the given SoundSet as default SoundSet for all blocks
    /// </summary>
    /// <param name="defaultSounds">SoundSet to use</param>
    void SetDefaultSounds(SoundSet defaultSounds);

    /// <summary>
    /// Gets a previously saved object from GlobalData
    /// </summary>
    /// <param name="name">The key to search for</param>
    /// <returns>The value at the given position</returns>
    byte[] GetGlobalData(string name);

    /// <summary>
    /// Store the given value to GlobalData. Data is persistent (will be stored in the savegame). Use carefully as big objects can cause problems
    /// </summary>
    /// <param name="name">Key value</param>
    /// <param name="value">Data to save</param>
    void SetGlobalData(string name, byte[] value);

    void RegisterOnLoad(Action f);
    void RegisterOnSave(Action f);

    /// <summary>
    /// Get the IP for the given player ID
    /// </summary>
    /// <param name="player"></param>
    /// <returns>IP of the given player</returns>
    string GetPlayerIp(int player);

    /// <summary>
    /// Get the player name for the given player ID
    /// </summary>
    /// <param name="player"></param>
    /// <returns>Name of the given player</returns>
    string GetPlayerName(int player);

    /// <summary>
    /// Set a special mod as requirement for the current mod. Use in PreStart() only.
    /// </summary>
    /// <param name="modname">Required mod</param>
    void RequireMod(string modname);

    /// <summary>
    /// Store the given value to GlobalDataNotSaved. Data is not persistent (will not be saved)
    /// </summary>
    /// <param name="name">Key value</param>
    /// <param name="value">Data to save</param>
    void SetGlobalDataNotSaved(string name, object value);

    /// <summary>
    /// Gets a previously saved object from GlobalDataNotSaved
    /// </summary>
    /// <param name="name">The key to search for</param>
    /// <returns>The value at the given position</returns>
    object GetGlobalDataNotSaved(string name);

    /// <summary>
    /// Send the given message to all players currently playing on the server. No formatting is done. Message is sent as given.
    /// </summary>
    /// <param name="message">The message to send</param>
    void SendMessageToAll(string message);

    /// <summary>
    /// Registers the given message to be displayed in /help
    /// </summary>
    /// <param name="command">Command for which the help string is intended</param>
    /// <param name="help">Short desciption of what the command does</param>
    void RegisterCommandHelp(string command, string help);

    /// <summary>
    /// Adds the given BlockType to the start inventory (blocks that each player on a survival server starts with)
    /// </summary>
    /// <param name="blocktype">Name of the blocktype</param>
    /// <param name="amount">Amount of blocks players get</param>
    void AddToStartInventory(string blocktype, int amount);

    long GetCurrentTick();

    /// <summary>
    /// Gets the number of real hours that one ingame day takes
    /// </summary>
    /// <returns>Duration of an ingame day</returns>
    double GetGameDayRealHours();

    /// <summary>
    /// Sets the number of real hours that one ingame day takes
    /// </summary>
    /// <param name="hours">Duration of an ingame day</param>
    void SetGameDayRealHours(double hours);

    void SetDaysPerYear(int days);
    int GetDaysPerYear();

    int GetSeason();

    /// <summary>
    /// Send current BlockType definitions to all players. Used on season change
    /// </summary>
    void UpdateBlockTypes();

    float GetPlayerPositionX(int player);
    float GetPlayerPositionY(int player);
    float GetPlayerPositionZ(int player);

    /// <summary>
    /// Sets the player's position on the server. Teleports a player to that position.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="x">x coordinate</param>
    /// <param name="y">y coordinate</param>
    /// <param name="z">z coordinate</param>
    void SetPlayerPosition(int player, float x, float y, float z);
    int GetPlayerHeading(int player);
    int GetPlayerPitch(int player);

    /// <summary>
    /// Sets the player's orientation
    /// </summary>
    /// <param name="player"></param>
    /// <param name="heading">The body heading. Value between 0 and 256</param>
    /// <param name="pitch">Head rotation. Value between 0 and 256</param>
    /// <param name="stance">Used for animation. Represents leaning left/right</param>
    void SetPlayerOrientation(int player, int heading, int pitch, int stance);

    /// <summary>
    /// Gets a list of all online players
    /// </summary>
    /// <returns>Array containing the IDs of online players</returns>
    int[] AllPlayers();

    void SetPlayerAreaSize(int size);
    void AddPermissionArea(int x1, int y1, int z1, int x2, int y2, int z2, int permissionLevel);
    void RemovePermissionArea(int x1, int y1, int z1, int x2, int y2, int z2);
    int GetPlayerPermissionLevel(int player);
    void SetCreative(bool creative);
    void SetWorldSize(int x, int y, int z);

    /// <summary>
    /// Returns the dimensions of the game window.
    /// </summary>
    /// <param name="player"></param>
    /// <returns>Array containing window size</returns>
    int[] GetScreenResolution(int player);

    void SendDialog(int player, string id, Dialog dialog);

    /// <summary>
    /// Changes the model and/or skin of the given player
    /// </summary>
    /// <param name="player"></param>
    /// <param name="model">Name of the model file (e.g. player.txt)</param>
    /// <param name="texture">Name of a texture file (should be present in data/public). If this is empty, default player skin will be used</param>
    void SetPlayerModel(int player, string model, string texture);

    void RenderHint(RenderHint hint);

    /// <summary>
    /// Changes freemove state of given player
    /// </summary>
    /// <param name="player"></param>
    /// <param name="enable">Enable (true) or disable (false) freemove and noclip for given player</param>
    void EnableFreemove(int player, bool enable);

    int GetPlayerHealth(int player);
    int GetPlayerMaxHealth(int player);
    void SetPlayerHealth(int player, int health, int maxhealth);

    /// <summary>
    /// Returns the default spawn position of a certain player.
    /// This method will return the custom spawnpoint if one has been permanently set.
    /// If no custom spawnpoint is present this method will return the global default spawnpoint.
    /// </summary>
    /// <param name="player">Player ID</param>
    /// <returns>Spawnpoint valid for the given player</returns>
    float[] GetDefaultSpawnPosition(int player);

    string ServerName { get; }

    string ServerMotd { get; }

    float[] MeasureTextSize(string text, DialogFont font);
    string ServerIp { get; }

    string ServerPort { get; }

    float GetPlayerPing(int player);

    /// <summary>
    /// Adds a new bot player to the game.
    /// </summary>
    /// <param name="name">Name for the new player</param>
    /// <returns>The ID of the newly added bot</returns>
    int AddBot(string name);
    bool IsBot(int player);
    void SetPlayerHeight(int player, float eyeheight, float modelheight);

    /// <summary>
    /// Disables use of given privilege for all players
    /// </summary>
    /// <param name="privilege">Privilege to be disabled</param>
    void DisablePrivilege(string privilege); //todo privileges

    /// <summary>
    /// Get the inventory data of the player
    /// </summary>
    /// <param name="player"></param>
    /// <returns>Inventory object</returns>
    Inventory GetInventory(int player);
    int GetActiveMaterialSlot(int player);

    /// <summary>
    /// This method is extremely buggy when (player != target)
    /// </summary>
    /// <param name="player"></param>
    /// <param name="target">ID of target player</param>
    /// <param name="tpp">Set camera mode to Third-Person-Camera (true/false)</param>
    void FollowPlayer(int player, int target, bool tpp);

    /// <summary>
    /// Set spectator status of the player
    /// </summary>
    /// <param name="player"></param>
    /// <param name="isSpectator">Player invisible to non-spectators (true) or visible for all (false)</param>
    void SetPlayerSpectator(int player, bool isSpectator);

    /// <summary>
    /// Get the BlockType object of a certain block ID. This method causes an exception when the ID is not found
    /// </summary>
    /// <param name="block">The block ID to search for</param>
    /// <returns>BlockType object</returns>
    BlockType GetBlockType(int block);

    /// <summary>
    /// Updates ammunition for given player
    /// </summary>
    /// <param name="player"></param>
    /// <param name="dictionary">Dictionary containing block ids and ammunition count</param>
    void NotifyAmmo(int player, Dictionary<int, int> dictionary);

    /// <summary>
    /// This allows all players to use the given privilege, no matter the normal configuration
    /// </summary>
    /// <param name="privilege">The privilege to grant</param>
    /// <param name="enable">Specifies if privilege shall be granted to all (true) or default behaviour should be used (false)</param>
    void EnableExtraPrivilegeToAll(string privilege, bool enable);

    /// <summary>
    /// Writes the given string into server event log
    /// </summary>
    /// <param name="serverEvent">log message</param>
    void LogServerEvent(string serverEvent);
    void SetWorldDatabaseReadOnly(bool readOnly);

    /// <summary>
    /// Sends an explosion to the player. This does not inflict damage. It just pushes the player.
    /// </summary>
    /// <param name="targetplayer">ID of target player</param>
    /// <param name="dx">X coordinate of explosion source</param>
    /// <param name="dy">Y coordinate of explosion source</param>
    /// <param name="dz">Z coordinate of explosion source</param>
    /// <param name="relativeposition">Specifies if the coordinates given are relative to the player</param>
    /// <param name="range">How far from center should the effect stop</param>
    /// <param name="time">How long the effect lasts</param>
    void SendExplosion(int targetplayer, float dx, float dy, float dz, bool relativeposition, float range, float time);

    /// <summary>
    /// Returns the color of the player group
    /// </summary>
    /// <param name="player"></param>
    /// <returns>A color string in format: &0</returns>
    string GetGroupColor(int player);

    /// <summary>
    /// Returns the name of the player group
    /// </summary>
    /// <param name="player"></param>
    /// <returns>A string containing the group name</returns>
    string GetGroupName(int player);

    List<string> required { get; set; }
}

public interface IMod
{
    /// <summary>
    /// Called once before the Mod is loaded. Use this to declare dependencies to other Mods.
    /// </summary>
    /// <param name="m">ModManager object</param>
    void PreStart(IServerModManager m);

    /// <summary>
    /// Called once when the Mod is started. Use this if you need to initialize fields, etc...
    /// </summary>
    /// <param name="m">ModManager object</param>
    void Start(IServerModManager m, IModEvents modEvents);
}