namespace MeinKraft;

// Non-cancelable events get a plain args class
public abstract class ModEventArgs
{
    // Common fields go here if needed (timestamp, etc.)
}

// Cancelable events extend this
public abstract class CancelableModEventArgs : ModEventArgs
{
    public bool IsCancelled { get; private set; }
    public void Cancel() => IsCancelled = true;
}

public interface IModEvents
{
    // ── Events ────────────────────────────────────────────────────────────────
    event Action<PlayerJoinArgs>? PlayerJoin;
    event Action<PlayerLeaveArgs>? PlayerLeave;
    event Action<PlayerDisconnectArgs>? PlayerDisconnect;
    event Action<BlockBuildArgs>? BlockBuild;
    event Action<BlockDeleteArgs>? BlockDelete;
    event Action<BlockUseArgs>? BlockUse;
    event Action<BlockUseWithToolArgs>? BlockUseWithTool;
    event Action<BlockUpdateArgs>? BlockUpdate;
    event Action<WorldGeneratorArgs>? WorldGenerator;
    event Action<PopulateChunkArgs>? PopulateChunk;
    event Action<CommandArgs>? Command;
    event Action<PlayerChatArgs>? PlayerChat;
    event Action<PlayerDeathArgs>? PlayerDeath;
    event Action<DialogClickArgs>? DialogClick;
    event Action<DialogClick2Args>? DialogClick2;
    event Action<WeaponHitArgs>? WeaponHit;
    event Action<WeaponShotArgs>? WeaponShot;
    event Action<SpecialKeyArgs>? SpecialKey;
    event Action<ChangedActiveMaterialSlotArgs>? ChangedActiveMaterialSlot;
    event Action<LoadWorldArgs>? LoadWorld;
    event Action<UpdateEntityArgs>? UpdateEntity;
    event Action<UseEntityArgs>? UseEntity;
    event Action<HitEntityArgs>? HitEntity;
    event Action<PermissionArgs>? Permission;
    event Action<CheckBlockBuildArgs>? CheckBlockBuild;
    event Action<CheckBlockDeleteArgs>? CheckBlockDelete;
    event Action<CheckBlockUseArgs>? CheckBlockUse;

    // ── Raisers ───────────────────────────────────────────────────────────────
    void RaisePlayerJoin(int player);
    void RaisePlayerLeave(int player);
    void RaisePlayerDisconnect(int player);
    void RaiseBlockBuild(int player, int x, int y, int z);
    void RaiseBlockDelete(int player, int x, int y, int z, int oldBlock);
    void RaiseBlockUse(int player, int x, int y, int z);
    void RaiseBlockUseWithTool(int player, int x, int y, int z, int tool);
    void RaiseBlockUpdate(int x, int y, int z);
    void RaiseWorldGenerator(int x, int y, int z, ushort[] chunk);
    void RaisePopulateChunk(int x, int y, int z);
    bool RaiseCommand(int player, string command, string argument);
    string? RaisePlayerChat(int player, string message, bool toTeam);
    void RaisePlayerDeath(int player, DeathReason reason, int sourceId);
    void RaiseDialogClick(int player, string widgetId);
    void RaiseDialogClick2(DialogClick2Args args);
    void RaiseWeaponHit(int sourcePlayer, int targetPlayer, int block, bool headshot);
    void RaiseWeaponShot(int sourcePlayer, int block);
    void RaiseSpecialKey(int player, SpecialKey key);
    void RaiseChangedActiveMaterialSlot(int player);
    void RaiseLoadWorld();
    void RaiseUpdateEntity(int chunkX, int chunkY, int chunkZ, int id);
    void RaiseUseEntity(int player, int chunkX, int chunkY, int chunkZ, int id);
    void RaiseHitEntity(int player, int chunkX, int chunkY, int chunkZ, int id);
    bool RaisePermission(PermissionArgs args);
    bool RaiseCheckBlockBuild(int player, int x, int y, int z);
    bool RaiseCheckBlockDelete(int player, int x, int y, int z);
    bool RaiseCheckBlockUse(int player, int x, int y, int z);
}

public sealed class ModEvents : IModEvents
{
    // ── Events ────────────────────────────────────────────────────────────────
    public event Action<PlayerJoinArgs>? PlayerJoin;
    public event Action<PlayerLeaveArgs>? PlayerLeave;
    public event Action<PlayerDisconnectArgs>? PlayerDisconnect;
    public event Action<BlockBuildArgs>? BlockBuild;
    public event Action<BlockDeleteArgs>? BlockDelete;
    public event Action<BlockUseArgs>? BlockUse;
    public event Action<BlockUseWithToolArgs>? BlockUseWithTool;
    public event Action<BlockUpdateArgs>? BlockUpdate;
    public event Action<WorldGeneratorArgs>? WorldGenerator;
    public event Action<PopulateChunkArgs>? PopulateChunk;
    public event Action<CommandArgs>? Command;
    public event Action<PlayerChatArgs>? PlayerChat;
    public event Action<PlayerDeathArgs>? PlayerDeath;
    public event Action<DialogClickArgs>? DialogClick;
    public event Action<DialogClick2Args>? DialogClick2;
    public event Action<WeaponHitArgs>? WeaponHit;
    public event Action<WeaponShotArgs>? WeaponShot;
    public event Action<SpecialKeyArgs>? SpecialKey;
    public event Action<ChangedActiveMaterialSlotArgs>? ChangedActiveMaterialSlot;
    public event Action<LoadWorldArgs>? LoadWorld;
    public event Action<UpdateEntityArgs>? UpdateEntity;
    public event Action<UseEntityArgs>? UseEntity;
    public event Action<HitEntityArgs>? HitEntity;
    public event Action<PermissionArgs>? Permission;
    public event Action<CheckBlockBuildArgs>? CheckBlockBuild;
    public event Action<CheckBlockDeleteArgs>? CheckBlockDelete;
    public event Action<CheckBlockUseArgs>? CheckBlockUse;

    // ── Raisers ───────────────────────────────────────────────────────────────
    public void RaisePlayerJoin(int player) =>
        PlayerJoin?.Invoke(new PlayerJoinArgs { Player = player });

    public void RaisePlayerLeave(int player) =>
        PlayerLeave?.Invoke(new PlayerLeaveArgs { Player = player });

    public void RaisePlayerDisconnect(int player) =>
        PlayerDisconnect?.Invoke(new PlayerDisconnectArgs { Player = player });

    public void RaiseBlockBuild(int player, int x, int y, int z) =>
        BlockBuild?.Invoke(new BlockBuildArgs { Player = player, X = x, Y = y, Z = z });

    public void RaiseBlockDelete(int player, int x, int y, int z, int oldBlock) =>
        BlockDelete?.Invoke(new BlockDeleteArgs { Player = player, X = x, Y = y, Z = z, OldBlock = oldBlock });

    public void RaiseBlockUse(int player, int x, int y, int z) =>
        BlockUse?.Invoke(new BlockUseArgs { Player = player, X = x, Y = y, Z = z });

    public void RaiseBlockUseWithTool(int player, int x, int y, int z, int tool) =>
        BlockUseWithTool?.Invoke(new BlockUseWithToolArgs { Player = player, X = x, Y = y, Z = z, Tool = tool });

    public void RaiseBlockUpdate(int x, int y, int z) =>
        BlockUpdate?.Invoke(new BlockUpdateArgs { X = x, Y = y, Z = z });

    public void RaiseWorldGenerator(int x, int y, int z, ushort[] chunk) =>
        WorldGenerator?.Invoke(new WorldGeneratorArgs { X = x, Y = y, Z = z, Chunk = chunk });

    public void RaisePopulateChunk(int x, int y, int z) =>
        PopulateChunk?.Invoke(new PopulateChunkArgs { X = x, Y = y, Z = z });

    public bool RaiseCommand(int player, string command, string argument)
    {
        var args = new CommandArgs { Player = player, Command = command, Argument = argument };
        Command?.Invoke(args);
        return args.Handled;
    }

    public string? RaisePlayerChat(int player, string message, bool toTeam)
    {
        var args = new PlayerChatArgs { Player = player, Message = message, ToTeam = toTeam, FinalMessage = message };
        PlayerChat?.Invoke(args);
        return args.FinalMessage;
    }

    public void RaisePlayerDeath(int player, DeathReason reason, int sourceId) =>
        PlayerDeath?.Invoke(new PlayerDeathArgs { Player = player, Reason = reason, SourceId = sourceId });

    public void RaiseDialogClick(int player, string widgetId) =>
        DialogClick?.Invoke(new DialogClickArgs { Player = player, WidgetId = widgetId });

    public void RaiseDialogClick2(DialogClick2Args args) =>
        DialogClick2?.Invoke(args);

    public void RaiseWeaponHit(int sourcePlayer, int targetPlayer, int block, bool headshot) =>
        WeaponHit?.Invoke(new WeaponHitArgs { SourcePlayer = sourcePlayer, TargetPlayer = targetPlayer, Block = block, Headshot = headshot });

    public void RaiseWeaponShot(int sourcePlayer, int block) =>
        WeaponShot?.Invoke(new WeaponShotArgs { SourcePlayer = sourcePlayer, Block = block });

    public void RaiseSpecialKey(int player, SpecialKey key) =>
        SpecialKey?.Invoke(new SpecialKeyArgs { Player = player, Key = key });

    public void RaiseChangedActiveMaterialSlot(int player) =>
        ChangedActiveMaterialSlot?.Invoke(new ChangedActiveMaterialSlotArgs { Player = player });

    public void RaiseLoadWorld() =>
        LoadWorld?.Invoke(new LoadWorldArgs());

    public void RaiseUpdateEntity(int chunkX, int chunkY, int chunkZ, int id) =>
        UpdateEntity?.Invoke(new UpdateEntityArgs { ChunkX = chunkX, ChunkY = chunkY, ChunkZ = chunkZ, Id = id });

    public void RaiseUseEntity(int player, int chunkX, int chunkY, int chunkZ, int id) =>
        UseEntity?.Invoke(new UseEntityArgs { Player = player, ChunkX = chunkX, ChunkY = chunkY, ChunkZ = chunkZ, Id = id });

    public void RaiseHitEntity(int player, int chunkX, int chunkY, int chunkZ, int id) =>
        HitEntity?.Invoke(new HitEntityArgs { Player = player, ChunkX = chunkX, ChunkY = chunkY, ChunkZ = chunkZ, Id = id });

    public bool RaisePermission(PermissionArgs args)
    {
        Action<PermissionArgs>? handlers = Permission;
        if (handlers is null)
        {
            return false;
        }

        foreach (Action<PermissionArgs> handler in handlers.GetInvocationList().Cast<Action<PermissionArgs>>())
        {
            handler(args);
            if (args.Allowed)
            {
                return true;
            }
        }

        return false;
    }

    public bool RaiseCheckBlockBuild(int player, int x, int y, int z) =>
        RaiseCancelable(CheckBlockBuild, new CheckBlockBuildArgs { Player = player, X = x, Y = y, Z = z });

    public bool RaiseCheckBlockDelete(int player, int x, int y, int z) =>
        RaiseCancelable(CheckBlockDelete, new CheckBlockDeleteArgs { Player = player, X = x, Y = y, Z = z });

    public bool RaiseCheckBlockUse(int player, int x, int y, int z) =>
        RaiseCancelable(CheckBlockUse, new CheckBlockUseArgs { Player = player, X = x, Y = y, Z = z });

    // ── Core cancelable dispatcher ─────────────────────────────────────────────
    private static bool RaiseCancelable<T>(Action<T>? evt, T args)
        where T : CancelableModEventArgs
    {
        if (evt is null)
        {
            return true;
        }

        foreach (Action<T> handler in evt.GetInvocationList().Cast<Action<T>>())
        {
            handler(args);
            if (args.IsCancelled)
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class BlockUseWithToolArgs : ModEventArgs
{
    public required int Player { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Z { get; init; }
    public required int Tool { get; init; }
}

public sealed class BlockUpdateArgs : ModEventArgs
{
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Z { get; init; }
}

public sealed class WorldGeneratorArgs : ModEventArgs
{
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Z { get; init; }
    public required ushort[] Chunk { get; init; }
}

public sealed class PopulateChunkArgs : ModEventArgs
{
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Z { get; init; }
}

public sealed class CommandArgs : ModEventArgs
{
    public required int Player { get; init; }
    public required string Command { get; init; }
    public required string Argument { get; init; }
    public bool Handled { get; set; }
}

public sealed class PlayerDeathArgs : ModEventArgs
{
    public required int Player { get; init; }
    public required DeathReason Reason { get; init; }
    public required int SourceId { get; init; }
}

public sealed class DialogClick2Args : ModEventArgs
{
    public required int Player { get; init; }
    public required string WidgetId { get; init; }
    public string[] TextBoxValue { get; set; }
}

public sealed class WeaponHitArgs : ModEventArgs
{
    public required int SourcePlayer { get; init; }
    public required int TargetPlayer { get; init; }
    public required int Block { get; init; }
    public required bool Headshot { get; init; }
}

public sealed class WeaponShotArgs : ModEventArgs
{
    public required int SourcePlayer { get; init; }
    public required int Block { get; init; }
}

public sealed class SpecialKeyArgs : ModEventArgs
{
    public required int Player { get; init; }
    public required SpecialKey Key { get; init; }
}

public sealed class ChangedActiveMaterialSlotArgs : ModEventArgs
{
    public required int Player { get; init; }
}

public sealed class LoadWorldArgs : ModEventArgs { }

public sealed class UpdateEntityArgs : ModEventArgs
{
    public required int ChunkX { get; init; }
    public required int ChunkY { get; init; }
    public required int ChunkZ { get; init; }
    public required int Id { get; init; }
}

public sealed class UseEntityArgs : ModEventArgs
{
    public required int Player { get; init; }
    public required int ChunkX { get; init; }
    public required int ChunkY { get; init; }
    public required int ChunkZ { get; init; }
    public required int Id { get; init; }
}

public sealed class HitEntityArgs : ModEventArgs
{
    public required int Player { get; init; }
    public required int ChunkX { get; init; }
    public required int ChunkY { get; init; }
    public required int ChunkZ { get; init; }
    public required int Id { get; init; }
}

public class PlayerDisconnectArgs : ModEventArgs
{
    public required int Player { get; init; }
}

public sealed class PlayerJoinArgs : ModEventArgs
{
    public required int Player { get; init; }
}

public sealed class PlayerLeaveArgs : ModEventArgs
{
    public required int Player { get; init; }
}

public sealed class BlockBuildArgs : ModEventArgs
{
    public required int Player { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Z { get; init; }
}

public sealed class BlockDeleteArgs : ModEventArgs
{
    public required int Player { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Z { get; init; }
    public required int OldBlock { get; init; }
}

public sealed class BlockUseArgs : ModEventArgs
{
    public required int Player { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Z { get; init; }
}

public sealed class PlayerChatArgs : ModEventArgs
{
    public required int Player { get; init; }
    public required string Message { get; init; }
    public required bool ToTeam { get; init; }
    // Mutable — handlers can rewrite the message
    public string FinalMessage { get; set; } = string.Empty;
}

// Cancelable
public sealed class CheckBlockBuildArgs : CancelableModEventArgs
{
    public required int Player { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Z { get; init; }
}

public sealed class CheckBlockDeleteArgs : CancelableModEventArgs
{
    public required int Player { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Z { get; init; }
}

public sealed class CheckBlockUseArgs : CancelableModEventArgs
{
    public required int Player { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Z { get; init; }
}