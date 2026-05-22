//This handles two types of environmental damage to the player:
//Block damage — every second, checks the block at eye level and the block at foot level.
//If either deals damage (like lava), it adds them together and calls ApplyDamageToPlayer.
//A timer ensures it only ticks once per second rather than every frame.
//Drowning — every second, if the player's eyes are underwater it decrements their oxygen.
//When oxygen hits zero it deals damage equal to 10% of max health. When they surface, oxygen instantly refills.
//The current oxygen level is also sent to the server if it's new enough to support that packet.

using MeinKraft;

/// <summary>
/// Handles environmental damage to the player from blocks (e.g. lava, fire) and drowning mechanics.
/// </summary>
public class ModBlockDamageToPlayer : ModBase
{
    public const int BlockDamageToPlayerEvery = 1;
    private int lastOxygenTickMilliseconds;
    private readonly DamageTimer blockDamageTimer;

    private readonly IGameWindowService _platform;
    private readonly IVoxelMap voxelMap;
    private readonly IBlockRegistry blockTypeRegistry;

    public ModBlockDamageToPlayer(IGameWindowService platform, IGame game, IVoxelMap voxelMap, IBlockRegistry blockTypeRegistry) : base(game)
    {
        _platform = platform;
        blockDamageTimer = new DamageTimer(BlockDamageToPlayerEvery, BlockDamageToPlayerEvery * 2);
        this.voxelMap = voxelMap;
        this.blockTypeRegistry = blockTypeRegistry;
    }

    public override void OnUpdate(float args)
    {
        if (Game.GuiState == GameState.MapLoading || Game.FollowId() != null)
        {
            return;
        }

        UpdateBlockDamageToPlayer(args);
    }

    private void UpdateBlockDamageToPlayer(float dt)
    {
        UpdateBlockDamage(dt);
        UpdateDrowning();
    }

    /// <summary>Applies damage from damaging blocks (e.g. lava) the player is standing in or near.</summary>
    private void UpdateBlockDamage(float dt)
    {
        float pX = Game.LocalPositionX;
        float pY = Game.LocalPositionY + Game.LocalEyeHeight;
        float pZ = Game.LocalPositionZ;

        int block1 = GetBlockAt(pX, pY, pZ);
        int block2 = GetBlockAt(pX, pY - 1, pZ);

        int damage = blockTypeRegistry.DamageToPlayer[block1] + blockTypeRegistry.DamageToPlayer[block2];
        if (damage <= 0)
        {
            return;
        }

        // Prefer eye-level block as damage source; fall back to feet block
        int hurtingBlock = (block1 != 0 && blockTypeRegistry.DamageToPlayer[block1] > 0) ? block1 : block2;
        int times = blockDamageTimer.Update(dt);
        for (int i = 0; i < times; i++)
        {
            Game.ApplyDamageToPlayer(damage, DeathReason.BlockDamage, hurtingBlock);
        }
    }

    /// <summary>Drains oxygen while underwater and applies drowning damage when oxygen is depleted.</summary>
    private void UpdateDrowning()
    {
        int deltaMs = _platform.TimeMillisecondsFromStart - lastOxygenTickMilliseconds;
        if (deltaMs < 1000)
        {
            return;
        }

        if (Game.WaterSwimmingEyes())
        {
            Game.PlayerStats.CurrentOxygen -= 1;
            if (Game.PlayerStats.CurrentOxygen <= 0)
            {
                Game.PlayerStats.CurrentOxygen = 0;
                int dmg = Math.Max(1, Game.PlayerStats.MaxHealth / 10);
                Game.ApplyDamageToPlayer(dmg, DeathReason.Drowning, 0);
            }
        }
        else
        {
            Game.PlayerStats.CurrentOxygen = Game.PlayerStats.MaxOxygen;
        }

        if (GameVersion.ServerVersionAtLeast(Game.ServerGameVersion, 2014, 3, 31))
        {
            Game.SendPacketClient(ClientPackets.Oxygen(Game.PlayerStats.CurrentOxygen));
        }

        lastOxygenTickMilliseconds = _platform.TimeMillisecondsFromStart;
    }

    private int GetBlockAt(float x, float y, float z)
    {
        int bx = (int)MathF.Floor(x);
        int by = (int)MathF.Floor(y);
        int bz = (int)MathF.Floor(z);
        return voxelMap.IsValidPos(bx, bz, by) ? voxelMap.GetBlock((int)x, (int)z, (int)y) : 0;
    }
}

public class DialogScreen : ModScreen
{
    public DialogScreen(IGameWindowService gameService, IGame game) : base(gameService, game)
    {
    }

    public override void OnButton(MenuWidget w)
    {
        if (w.Isbutton)
        {
            string[] textValues = new string[WidgetCount];
            for (int i = 0; i < WidgetCount; i++)
            {
                string s = widgets[i].Text;
                s ??= "";
                textValues[i] = s;
            }

            Game.SendPacketClient(ClientPackets.DialogClick(w.Id, textValues, WidgetCount));
        }
    }
}

public class DamageTimer
{
    public float Interval { get; }
    public float? MaxDeltaTime { get; }

    private float _accumulator;

    public DamageTimer(float interval, float? maxDeltaTime = null)
    {
        if (interval <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }

        Interval = interval;
        MaxDeltaTime = maxDeltaTime;
    }

    public void Reset() => _accumulator = 0;

    public int Update(float dt)
    {
        if (dt < 0)
        {
            return 0; // or throw, depending on your engine philosophy
        }

        _accumulator += dt;

        if (MaxDeltaTime.HasValue && _accumulator > MaxDeltaTime.Value)
        {
            _accumulator = MaxDeltaTime.Value;
        }

        int updates = (int)(_accumulator / Interval);
        _accumulator -= updates * Interval;

        return updates;
    }
}