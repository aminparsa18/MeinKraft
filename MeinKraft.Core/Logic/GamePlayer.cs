using MeinKraft;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

public partial class Game
{
    // -------------------------------------------------------------------------
    // Eyes position
    // -------------------------------------------------------------------------

    /// <summary>World-space X of the player's eye point.</summary>
    public float EyesPosX => Player.Position.X;

    /// <summary>
    /// World-space Y of the player's eye point.
    /// Offset above the entity origin by <see cref="GetCharacterEyesHeight"/>.
    /// </summary>
    public float EyesPosY => Player.Position.Y + GetCharacterEyesHeight();

    /// <summary>World-space Z of the player's eye point.</summary>
    public float EyesPosZ => Player.Position.Z;

    /// <summary>Returns the eye height of the local player's draw model in world units.</summary>
    public float GetCharacterEyesHeight() => Entities[LocalPlayerId].DrawModel.EyeHeight;

    /// <summary>Sets the eye height of the local player's draw model.</summary>
    public void SetCharacterEyesHeight(float value) => Entities[LocalPlayerId].DrawModel.EyeHeight = value;

    /// <summary>Block X coordinate at the player's eye level.</summary>
    public int PlayerEyesBlockX => (int)MathF.Floor(EyesPosX);

    /// <summary>
    /// Block Y coordinate at the player's eye level.
    /// Note: maps EyesPosZ → block Y due to the engine's Z-up convention.
    /// </summary>
    public int PlayerEyesBlockY => (int)MathF.Floor(EyesPosZ);

    /// <summary>
    /// Block Z coordinate at the player's eye level.
    /// Note: maps EyesPosY → block Z due to the engine's Z-up convention.
    /// </summary>
    public int PlayerEyesBlockZ => (int)MathF.Floor(EyesPosY);

    /// <summary>
    /// Returns the block type at the player's eye position, or
    /// <c>-1</c> when out of bounds below the water line, or
    /// <c>0</c> (air) when out of bounds above it.
    /// </summary>
    internal int GetPlayerEyesBlock()
    {
        int bx = (int)MathF.Floor(EyesPosX);
        int by = (int)MathF.Floor(EyesPosZ);
        int bz = (int)MathF.Floor(EyesPosY);

        if (!_voxelMap.IsValidPos(bx, by, bz))
        {
            return Player.Position.Y < WaterLevel() ? -1 : 0;
        }

        return _voxelMap.GetBlockValid(bx, by, bz);
    }

    // -------------------------------------------------------------------------
    // Swimming state
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns <see langword="true"/> when the player's eyes are inside a fluid
    /// block, or when the eye position is out of bounds below the water line.
    /// </summary>
    public bool SwimmingEyes()
    {
        int eyesBlock = GetPlayerEyesBlock();
        if (eyesBlock == -1)
        {
            return true;
        }

        return _blockRegistry.WalkableType[eyesBlock] == WalkableType.Fluid;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the block one unit above the player's
    /// feet is a fluid, indicating the body is submerged.
    /// </summary>
    public bool SwimmingBody()
    {
        int block = _voxelMap.GetBlock(
            (int)Player.Position.X,
            (int)Player.Position.Z,
            (int)(Player.Position.Y + 1));

        if (block == -1)
        {
            return true;
        }

        return _blockRegistry.WalkableType[block] == WalkableType.Fluid;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the player's eyes are specifically
    /// inside a water block (as opposed to any fluid such as lava).
    /// </summary>
    public bool WaterSwimmingEyes()
    {
        int block = GetPlayerEyesBlock();
        if (block == -1)
        {
            return true;
        }

        return _blockRegistry.IsWater(block);
    }

    // -------------------------------------------------------------------------
    // Block under / in hand
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the block type directly below the player's feet,
    /// or <c>-1</c> when the position is out of map bounds.
    /// </summary>
    public int BlockUnderPlayer()
    {
        if (!_voxelMap.IsValidPos(
                (int)Player.Position.X,
                (int)Player.Position.Z,
                (int)Player.Position.Y - 1))
        {
            return -1;
        }

        return _voxelMap.GetBlock(
            (int)Player.Position.X,
            (int)Player.Position.Z,
            (int)Player.Position.Y - 1);
    }

    /// <summary>
    /// Returns the block ID of the item currently held in the active hand slot,
    /// or <see langword="null"/> if the slot is empty or holds a non-block item.
    /// </summary>
    public int? BlockInHand()
    {
        InventoryItem item = Inventory.RightHand[ActiveMaterial];
        return item != null && item.InventoryItemType == InventoryItemType.Block
            ? item.BlockId
            : null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the active hand slot contains any item
    /// (weapon, tool, or block).
    /// </summary>
    internal bool IsWearingWeapon() => Inventory.RightHand[ActiveMaterial] != null;

    // -------------------------------------------------------------------------
    // Movement speed
    // -------------------------------------------------------------------------

    /// <summary>
    /// Computes the effective movement speed for this frame by applying
    /// floor-surface, modifier-key, held-item, and iron-sights multipliers
    /// to <see cref="MoveSpeed"/>.
    /// </summary>
    public float MoveSpeedNow()
    {
        float speed = MoveSpeed;

        int blockUnder = BlockUnderPlayer();
        if (blockUnder != -1)
        {
            float floorSpeed = _blockRegistry.WalkSpeed[blockUnder];
            if (floorSpeed != 0)
            {
                speed *= floorSpeed;
            }
        }

        if (KeyboardState[GetKey(Keys.LeftControl)])
        {
            speed *= 2f / 10f;   // Ctrl = slow walk
        }
        else if (KeyboardState[GetKey(Keys.LeftShift)])
        {
            // Shift = sprint; faster multiplier in freemove/noclip mode.
            speed *= FreemoveLevel == FreeMoveLevel.Freemove ? 4f : 2f;
        }

        InventoryItem item = Inventory.RightHand[ActiveMaterial];
        if (!_blockRegistry.BlockTypes.TryGetValue(item.BlockId, out var blockType))
            return Basemovespeed; // block types not yet received, use default

        float itemSpeed = blockType.WalkSpeedWhenUsed;
        if (itemSpeed != 0)
        {
            speed *= itemSpeed;
        }

        if (IronSights)
        {
            float ironSpeed = _blockRegistry.BlockTypes[item.BlockId].IronSightsMoveSpeed;
            if (ironSpeed != 0)
            {
                speed *= ironSpeed;
            }
        }

        return speed;
    }

    // -------------------------------------------------------------------------
    // Weapon / aim
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the current field of view in radians. When iron sights are active
    /// and the held item defines an <c>IronSightsFov</c> multiplier, the base
    /// FOV is scaled down to simulate zoom.
    /// </summary>
    public float CurrentFov()
    {
        if (IronSights)
        {
            InventoryItem item = Inventory.RightHand[ActiveMaterial];
            if (item != null && item.InventoryItemType == InventoryItemType.Block)
            {
                float ironFov = _blockRegistry.BlockTypes[item.BlockId].IronSightsFov;
                if (ironFov != 0)
                    return (fov + FovOffset) * ironFov;
            }
        }
        return fov + FovOffset;
    }

    /// <summary>
    /// Returns the recoil value of the item currently held in the active hand slot,
    /// or <c>0</c> if the slot is empty or holds a non-block item.
    /// </summary>
    public float CurrentRecoil()
    {
        InventoryItem item = Inventory.RightHand[ActiveMaterial];
        if (item == null || item.InventoryItemType != InventoryItemType.Block)
        {
            return 0;
        }

        return _blockRegistry.BlockTypes[item.BlockId].Recoil;
    }

    /// <summary>
    /// Returns the aim spread radius in screen pixels for the current frame,
    /// factoring in iron-sights mode and player velocity relative to
    /// <see cref="MoveSpeed"/>.
    /// </summary>
    public float CurrentAimRadius()
    {
        InventoryItem item = Inventory.RightHand[ActiveMaterial];
        if (item == null || item.InventoryItemType != InventoryItemType.Block)
        {
            return 0;
        }

        float radius = IronSights
            ? _blockRegistry.BlockTypes[item.BlockId].IronSightsAimRadius / 800f * gameService.CanvasWidth
            : _blockRegistry.BlockTypes[item.BlockId].AimRadius / 800f * gameService.CanvasWidth;

        return radius + (RadiusWhenMoving * radius * Math.Min(playervelocity.Length / MoveSpeed, 1));
    }

    // -------------------------------------------------------------------------
    // Damage
    // -------------------------------------------------------------------------

    /// <summary>
    /// Applies <paramref name="damage"/> to the local player's health.
    /// Plays the appropriate audio cue and sends health/death packets to the server.
    /// </summary>
    /// <param name="damage">Amount of health to subtract.</param>
    /// <param name="damageSource">Cause of death, used in the death packet.</param>
    /// <param name="sourceId">Entity ID of the damage source.</param>
    public void ApplyDamageToPlayer(int damage, DeathReason damageSource, int sourceId)
    {
        PlayerStats.CurrentHealth -= damage;
        if (PlayerStats.CurrentHealth <= 0)
        {
            PlayerStats.CurrentHealth = 0;
            PlayAudio("death.wav");
            SendPacketClient(ClientPackets.Death(damageSource, sourceId));
        }
        else
        {
            PlayAudio(rnd.Next() % 2 == 0 ? "grunt1.wav" : "grunt2.wav");
        }

        SendPacketClient(ClientPackets.Health(PlayerStats.CurrentHealth));
    }

    // -------------------------------------------------------------------------
    // Player/entity collision
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns <see langword="true"/> when any loaded entity (including the local
    /// player) occupies the block at the given world position.
    /// Used to prevent placing blocks inside players.
    /// </summary>
    public bool IsAnyPlayerInPos(int blockposX, int blockposY, int blockposZ)
    {
        for (int i = 0; i < Entities.Count; i++)
        {
            Entity e = Entities[i];
            if (e?.DrawModel == null)
            {
                continue;
            }

            if (e.NetworkPosition == null || e.NetworkPosition.PositionLoaded)
            {
                if (IsPlayerInPos(e.Position.X, e.Position.Y, e.Position.Z,
                    blockposX, blockposY, blockposZ, e.DrawModel.ModelHeight))
                {
                    return true;
                }
            }
        }

        return IsPlayerInPos(Player.Position.X, Player.Position.Y, Player.Position.Z,
            blockposX, blockposY, blockposZ, Player.DrawModel.ModelHeight);
    }

    /// <summary>
    /// Tests whether a single entity's bounding column overlaps the given block.
    /// Iterates one check per integer height unit of the model.
    /// </summary>
    /// <remarks>
    /// The axis order passed to <see cref="ScriptCharacterPhysics.BoxPointDistance"/>
    /// is (X, Z, Y) — intentional, matching the engine's Z-up world convention.
    /// </remarks>
    private bool IsPlayerInPos(float playerposX, float playerposY, float playerposZ,
        int blockposX, int blockposY, int blockposZ, float playerHeight)
    {
        for (int i = 0; i < MathF.Floor(playerHeight) + 1; i++)
        {
            if (ScriptCharacterPhysics.BoxPointDistance(
                blockposX, blockposZ, blockposY,
                blockposX + 1, blockposZ + 1, blockposY + 1,
                playerposX, playerposY + i + WallDistance, playerposZ) < WallDistance)
            {
                return true;
            }
        }

        return false;
    }
}