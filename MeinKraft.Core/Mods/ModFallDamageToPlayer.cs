using MeinKraft;

/// <summary>
/// Applies fall damage and manages the falling wind sound effect for the local player.
/// </summary>
public class ModFallDamageToPlayer : ModBase
{
    private const float FallDamageCooldownSeconds = 1f;
    private const float SpawnGracePeriodSeconds = 5f;

    private bool _wasSpawned;
    private bool fallSoundPlaying;
    private int lastFallDamageTimeMilliseconds;
    private readonly IGameWindowService _gameService;
    private readonly IVoxelMap voxelMap;
    private readonly IBlockRegistry _blockRegistry;

    public ModFallDamageToPlayer(IGameWindowService platform, IVoxelMap voxelMap, IGame game, IBlockRegistry blockRegistry) : base(game)
    {
        this._gameService = platform;
        this.voxelMap = voxelMap;
        _blockRegistry = blockRegistry;
    }

    public override void OnUpdate(float args)
    {
        if (Game.GuiState == GameState.MapLoading)
        {
            return;
        }

        // Detect spawn — push the damage timer into the future so the
        // first landing after spawn is always within the grace period.
        if (Game.Spawned && !_wasSpawned)
        {
            lastFallDamageTimeMilliseconds = _gameService.TimeMillisecondsFromStart
                + (int)(SpawnGracePeriodSeconds * 1000);
        }
        _wasSpawned = Game.Spawned;

        if (Game.Controls.FreeMove)
        {
            if (fallSoundPlaying)
            {
                SetFallSoundActive(false);
            }

            return;
        }

        if (Game.FollowId() == null)
        {
            UpdateFallDamageToPlayer(args);
        }
    }

    internal void UpdateFallDamageToPlayer(float dt)
    {
        if (!Game.Spawned)
        {
            return;
        }

        float fallSpeed = Game.MovedZ / -Game.Basemovespeed;

        int posX = Game.PlayerEyesBlockX;
        int posY = Game.PlayerEyesBlockY;
        int posZ = Game.PlayerEyesBlockZ;
        // Play falling wind sound when high up or falling fast
        bool highUp = Game.Blockheight(posX, posY, posZ) < posZ - 8;
        SetFallSoundActive((highUp || fallSpeed > 3) && fallSpeed > 2);

        ApplyFallDamage(posX, posY, posZ, fallSpeed);
    }

    private void ApplyFallDamage(int posX, int posY, int posZ, float fallSpeed)
    {
        if (!Game.IsPlayerOnGround)
        {
            return;  // only damage on landing
        }

        if (fallSpeed < 4f)
        {
            return;
        }

        if (!voxelMap.IsValidPos(posX, posY, posZ - 3))
        {
            return;
        }

        int blockBelow = voxelMap.GetBlock(posX, posY, posZ - 3);
        if (blockBelow == 0 || _blockRegistry.IsWater(blockBelow))
        {
            return;
        }

        // fallspeed 4 ≈ 10 blocks high, 5.5 ≈ 20 blocks high
        float severity = fallSpeed switch
        {
            < 4.5f => 0.3f,
            < 5.0f => 0.5f,
            < 5.5f => 0.6f,
            < 6.0f => 0.8f,
            _ => 1.0f
        };

        int now = _gameService.TimeMillisecondsFromStart;
        if ((now - lastFallDamageTimeMilliseconds) / 1000f < FallDamageCooldownSeconds)
        {
            return;
        }

        lastFallDamageTimeMilliseconds = now;
        Game.ApplyDamageToPlayer((int)(severity * Game.PlayerStats.MaxHealth), DeathReason.FallDamage, 0);
    }

    internal void SetFallSoundActive(bool active)
    {
        Game.AudioPlayLoop("fallloop.wav", active, true);
        fallSoundPlaying = active;
    }
}