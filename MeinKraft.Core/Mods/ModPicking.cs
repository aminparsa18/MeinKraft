using MeinKraft;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

/// <summary>
/// Handles block and entity picking (ray-casting from the player's view),
/// block placement/destruction, weapon shooting, grenade throwing, and the
/// cuboid fill-area tool.
/// </summary>
public class ModPicking : ModBase
{
    /// <summary>Reusable viewport rectangle passed to <see cref="VectorUtils.UnProject"/>.</summary>
    private readonly int[] _tempViewport;

    /// <summary>
    /// Tracks which world blocks were overwritten by the fill-area tool so they
    /// can be restored if the fill is cancelled.
    /// Keyed by (x, y, z) block position; value is the original block ID.
    /// </summary>
    internal Dictionary<(int x, int y, int z), float> fillarea;

    /// <summary>First corner of the pending cuboid fill selection, or <see langword="null"/> when unset.</summary>
    internal Vector3i? fillstart;

    /// <summary>Second corner of the pending cuboid fill selection, or <see langword="null"/> when unset.</summary>
    internal Vector3i? fillend;

    /// <summary>Timestamp (ms) of the last block build/destroy action, used to enforce <see cref="BuildDelay"/>.</summary>
    internal int lastbuildMilliseconds;

    /// <summary>
    /// <see langword="true"/> when the mouse button was released between clicks,
    /// allowing instant action on the next press.
    /// </summary>
    internal bool fastclicking;

    // ── Block-break physics particle settings ─────────────────────────────────

    /// <summary>Downward acceleration applied to airborne particles (world units / s²).</summary>
    private const float ParticleGravity = 80f;

    /// <summary>Fraction of vertical speed kept after each ground bounce (0 = no bounce, 1 = perfect).</summary>
    private const float ParticleBounce = 0.38f;

    /// <summary>Fraction of horizontal speed kept each time a particle lands.</summary>
    private const float ParticleFriction = 0.70f;

    /// <summary>Upward velocity below which a bouncing particle is considered at rest.</summary>
    private const float MinBounceVelocity = 0.9f;

    /// <summary>How many block-face particles to spawn per break.</summary>
    private const int ParticleCount = 14;

    /// <summary>
    /// Carries the per-frame physics state of a single break particle.
    /// <c>Ent</c> is the live entity whose sprite position we update in place
    /// every frame — the entity system handles rendering and expiry automatically.
    /// </summary>
    private struct PhysicsParticle
    {
        /// <summary>Reference to the sprite entity; we write positionX/Y/Z each frame.</summary>
        public Entity Ent;

        /// <summary>Current velocity vector (Y is up, same convention as render / camera space).</summary>
        public float VX, VY, VZ;

        /// <summary>Seconds of life remaining — mirrors <c>Ent.expires</c> so we can remove from the list.</summary>
        public float Life;

        /// <summary>True once the particle has settled on a surface and stopped moving.</summary>
        public bool Grounded;
    }

    /// <summary>All currently active physics particles, updated every draw frame.</summary>
    private readonly List<PhysicsParticle> _physicsParticles = new(64);

    private readonly IGameWindowService platform;
    private readonly IVoxelMap voxelMap;
    private readonly ICameraService cameraService;
    private readonly IMeshDrawer meshDrawer;
    private readonly IMeshBatcher _meshBatcher;
    private readonly IModRegistry modRegistry;
    private readonly IBlockRegistry blockTypeRegistry;
    private readonly ITerrainChunkTesselator _terrainChunkTesselator;
    private readonly IGameLogger _gameLogger;
    private readonly Random random;

    public ModPicking(IGameWindowService platform, IVoxelMap voxelMap, ICameraService cameraService,
        ITerrainChunkTesselator terrainChunkTesselator, IMeshBatcher meshBatcher, IGameLogger gameLogger,
        IMeshDrawer meshDrawer, IModRegistry modRegistry, IBlockRegistry blockTypeRegistry, IGame game) : base(game)
    {
        this.platform = platform;
        this.voxelMap = voxelMap;
        this.cameraService = cameraService;
        this.meshDrawer = meshDrawer;
        this.modRegistry = modRegistry;
        this.blockTypeRegistry = blockTypeRegistry;
        _gameLogger = gameLogger;
        _terrainChunkTesselator = terrainChunkTesselator;
        this._meshBatcher = meshBatcher;
        _tempViewport = new int[4];
        fillarea = new();
        random = new Random();
    }

    /// <inheritdoc/>
    public override void OnFrame(float deltaTime)
    {
        if (Game.GuiState == GameState.Normal)
        {
            UpdatePicking();
        }
    }

    /// <inheritdoc/>
    public override void OnRender3d(float dt) 
        => UpdateParticlePhysics(dt);

    private bool _mouseLeftHeld;
    private bool _mouseMiddleHeld;
    private bool _mouseRightHeld;
    private bool _mouseLeftClick;    // edge: pressed this event
    private bool _mouseLeftRelease;  // edge: released this event
    private bool _mouseRightClick;   // edge: pressed this event

    /// <inheritdoc/>
    public override void OnMouseUp(MouseEventArgs args)
    {
        if (args.Button == (int)MouseButton.Left) { _mouseLeftHeld = false; _mouseLeftRelease = true; }
        if (args.Button == (int)MouseButton.Middle) _mouseMiddleHeld = false;
        if (args.Button == (int)MouseButton.Right) _mouseRightHeld = false;

        if (Game.GuiState == GameState.Normal)
        {
            UpdatePicking();
        }
    }

    /// <inheritdoc/>
    public override void OnMouseDown(MouseEventArgs args)
    {
        if (args.Button == (int)MouseButton.Left) { _mouseLeftHeld = true; _mouseLeftClick = true; }
        if (args.Button == (int)MouseButton.Middle) _mouseMiddleHeld = true;
        if (args.Button == (int)MouseButton.Right) { _mouseRightHeld = true; _mouseRightClick = true; }
        if (Game.GuiState == GameState.Normal)
        {
            UpdatePicking();
            UpdateEntityHit();
        }
    }

    /// <summary>
    /// Main picking entry point. Clears the selected block when the player is
    /// following an entity (spectator), otherwise begins the bullet/block trace.
    /// </summary>
    internal void UpdatePicking()
    {
        if (Game.FollowId() != null)
        {
            Game.SelectedBlockPositionX = -1;
            Game.SelectedBlockPositionY = -1;
            Game.SelectedBlockPositionZ = -1;
            return;
        }

        NextBullet(bulletsShot: 0);

        // Edge triggers are single-consumption — cleared after the full burst resolves.
        _mouseLeftClick = false;
        _mouseLeftRelease = false;
        _mouseRightClick = false;
    }

    /// <summary>
    /// Performs a single picking trace, handles mouse-button state, weapon logic,
    /// grenade cooking, block interaction, and entity hit detection.
    /// Calls itself recursively for burst-fire weapons.
    /// </summary>
    /// <param name="game">Current game instance.</param>
    /// <param name="bulletsShot">Number of bullets already fired in this burst.</param>
    internal void NextBullet(int bulletsShot)
    {
        bool left = _mouseLeftHeld;
        bool middle = _mouseMiddleHeld;
        bool right = _mouseRightHeld;
        bool isNextShot = bulletsShot != 0;

        // leftpressedpicking latch is gone — _mouseLeftHeld is authoritative.
        if (!left)
            Game.CurrentAttackedBlock = null;

        InventoryItem item = Game.Inventory.RightHand[Game.ActiveMaterial];
        bool isPistol = item != null && blockTypeRegistry.BlockTypes[item.BlockId].IsPistol;
        bool isGrenade = isPistol && blockTypeRegistry.BlockTypes[item.BlockId].PistolType == PistolType.Grenade;
        bool isPistolShoot = isPistol && left;
        if (isPistol && isGrenade)
            isPistolShoot = _mouseLeftRelease;

        // Grenade cooking — start timer on left-press, auto-fire when cooked.
        // TODO: fix instant explosion when closing ESC menu.
        if (_mouseLeftClick)
        {
            Game.grenadecookingstartMilliseconds = platform.TimeMillisecondsFromStart;
            if (isPistol && isGrenade && blockTypeRegistry.BlockTypes[item.BlockId].Sounds.Shoot.Length > 0)
                Game.PlayAudio($"{blockTypeRegistry.BlockTypes[item.BlockId].Sounds.Shoot[0]}.ogg");
        }

        float cookWait = (platform.TimeMillisecondsFromStart - Game.grenadecookingstartMilliseconds) / 1000f;
        if (isGrenade && left)
        {
            if (cookWait >= Game.grenadetime && Game.grenadecookingstartMilliseconds != 0)
            {
                isPistolShoot = true;
                _mouseLeftRelease = true;   // simulate release to trigger grenade throw
            }
            else
            {
                return;
            }
        }
        else
        {
            Game.grenadecookingstartMilliseconds = 0;
        }

        // Iron sights toggle (right-click with pistol, 500 ms cooldown).
        if (isPistol && _mouseRightClick
         && (platform.TimeMillisecondsFromStart - Game.lastironsightschangeMilliseconds) >= 500)
        {
            Game.IronSights = !Game.IronSights;
            Game.lastironsightschangeMilliseconds = platform.TimeMillisecondsFromStart;
        }

        Line3D pick = new();
        GetPickingLine(pick, isPistolShoot);
        ArraySegment<BlockPosSide> pick2 = Game.Pick(cameraService.BlockOctreeSearcher, pick, out int pick2count);

        if (left) Game.handSetAttackDestroy = true;
        else if (right) Game.handSetAttackBuild = true;

        // Overhead camera: walk toward clicked block.
        if (Game.OverheadCamera && pick2count > 0 && left && Game.Follow == null)
            Game.PlayerDestination = new Vector3(pick2[0].BlockPos[0], pick2[0].BlockPos[1] + 1, pick2[0].BlockPos[2]);

        // Distance check.
        bool pickDistanceOk = pick2count > 0;
        if (pickDistanceOk)
        {
            float pickDist = Vector3.Distance(
                new Vector3(pick2[0].BlockPos[0] + 0.5f, pick2[0].BlockPos[1] + 0.5f, pick2[0].BlockPos[2] + 0.5f),
                new Vector3(pick.Start[0], pick.Start[1], pick.Start[2]));
            if (pickDist > CurrentPickDistance())
                pickDistanceOk = false;
        }

        bool playerTileEmpty = Game.IsTileEmptyForPhysics(
            (int)Game.Player.Position.X, (int)Game.Player.Position.Z, (int)(Game.Player.Position.Y + 0.5f));
        bool playerTileEmptyClose = Game.IsTileEmptyForPhysicsClose(
            (int)Game.Player.Position.X, (int)Game.Player.Position.Z, (int)(Game.Player.Position.Y + 0.5f));

        BlockPosSide pick0 = new();
        if (pick2count > 0 && ((pickDistanceOk && (playerTileEmpty || playerTileEmptyClose)) || Game.OverheadCamera))
        {
            Game.SelectedBlockPositionX = (int)pick2[0].Current()[0];
            Game.SelectedBlockPositionY = (int)pick2[0].Current()[1];
            Game.SelectedBlockPositionZ = (int)pick2[0].Current()[2];
            pick0 = pick2[0];
        }
        else
        {
            Game.SelectedBlockPositionX = -1;
            Game.SelectedBlockPositionY = -1;
            Game.SelectedBlockPositionZ = -1;
            pick0.BlockPos = new(-1, -1, -1);
        }

        PickEntity(pick, pick2, pick2count);

        if (Game.CameraType is CameraType.Fpp or CameraType.Tpp)
        {
            int ntileX = (int)pick0.Current()[0];
            int ntileY = (int)pick0.Current()[1];
            int ntileZ = (int)pick0.Current()[2];
            if (blockTypeRegistry.IsUsableBlock(voxelMap.GetBlock(ntileX, ntileZ, ntileY)))
                Game.CurrentAttackedBlock = new Vector3i(ntileX, ntileZ, ntileY);
        }

        if (Game.GetFreeMouse())
        {
            if (pick2count > 0)
                OnPick_(pick0);
            return;
        }

        bool buildDelayElapsed = (platform.TimeMillisecondsFromStart - lastbuildMilliseconds) / 1000f >= BuildDelay();
        if (!buildDelayElapsed && !isNextShot)
        {
            PickingEnd(left, right, middle, isPistol);
            return;
        }

        if (left && Game.Inventory.RightHand[Game.ActiveMaterial] == null)
            Game.SendPacketClient(ClientPackets.MonsterHit(2 + (random.Next() * 4)));

        if ((left || right || middle) && !isGrenade)
            lastbuildMilliseconds = platform.TimeMillisecondsFromStart;

        if (isGrenade && _mouseLeftRelease)
            lastbuildMilliseconds = platform.TimeMillisecondsFromStart;

        if (Game.ReloadStartMilliseconds != 0)
        {
            PickingEnd(left, right, middle, isPistol);
            return;
        }

        if (isPistolShoot)
        {
            if (!(Game.LoadedAmmo[item.BlockId] > 0) || !(Game.TotalAmmo[item.BlockId] > 0))
            {
                Game.PlayAudio("Dry Fire Gun-SoundBible.com-2053652037.ogg");
                PickingEnd(left, right, middle, isPistol);
                return;
            }

            FirePistol(pick, pick2, pick2count, item, isGrenade, cookWait, ref bulletsShot);
            PickingEnd(left, right, middle, isPistol);
            return;
        }

        if (isPistol && right)
        {
            PickingEnd(left, right, middle, isPistol);
            return;
        }

        if (pick2count > 0)
            HandleBlockInteraction(pick0, pick2, pick2count, left, right, middle, isPistol, isGrenade);

        PickingEnd(left, right, middle, isPistol);
    }

    /// <summary>
    /// Handles all block interactions: middle-click clone, left-click destroy,
    /// and right-click place.
    /// </summary>
    private void HandleBlockInteraction(BlockPosSide pick0,
        ArraySegment<BlockPosSide> pick2, int pick2count,
        bool left, bool right, bool middle, bool isPistol, bool isGrenade)
    {
        if (middle)
        {
            HandleMiddleClickClone(pick0);
        }

        if (left || right)
        {
            int newtileX = right ? (int)pick0.Translated()[0] : (int)pick0.Current()[0];
            int newtileY = right ? (int)pick0.Translated()[1] : (int)pick0.Current()[1];
            int newtileZ = right ? (int)pick0.Translated()[2] : (int)pick0.Current()[2];

            if (!voxelMap.IsValidPos(newtileX, newtileZ, newtileY))
            {
                return;
            }

            bool pickIsInvalid = pick0.BlockPos[0] == -1 && pick0.BlockPos[1] == -1 && pick0.BlockPos[2] == -1;
            if (!pickIsInvalid)
            {
                int blocktype = left
                    ? voxelMap.GetBlock(newtileX, newtileZ, newtileY)
                    : (Game.BlockInHand() == null ? 1 : Game.BlockInHand() ?? -1);

                if (left && blocktype == blockTypeRegistry.BlockIdAdminium)
                {
                    PickingEnd(left, right, middle, isPistol);
                    return;
                }

                string[] sound = left ? blockTypeRegistry.BreakSound[blocktype] : blockTypeRegistry.BuildSound[blocktype];
                if (sound != null)
                {
                    Game.PlayAudio(sound[0]);
                } // TODO: sound cycle
            }

            if (!right)
            {
                HandleAttack(pick0, newtileX, newtileZ, newtileY, left, right, middle, isPistol);
                return;
            }

            if (!voxelMap.IsValidPos(newtileX, newtileZ, newtileY))
            {
                throw new ArgumentException("Error in picking - NextBullet()");
            }

            OnPick(newtileX, newtileZ, newtileY,
                (int)pick0.Current()[0], (int)pick0.Current()[2], (int)pick0.Current()[1],
                pick0.CollisionPos, right);
        }
    }

    /// <summary>
    /// Handles a left-click attack on a block: reduces block health and destroys
    /// it when health reaches zero.
    /// </summary>
    private void HandleAttack(BlockPosSide tile,
        int newtileX, int newtileY, int newtileZ,
        bool left, bool right, bool middle, bool isPistol)
    {
        int posx = newtileX;
        int posy = newtileY;
        int posz = newtileZ;
        Game.CurrentAttackedBlock = new Vector3i(posx, posy, posz);
        (int posx, int posy, int posz) key = (posx, posy, posz);

        if (!Game.BlockHealth.ContainsKey(key))
        {
            Game.BlockHealth[key] = Game.GetCurrentBlockHealth(posx, posy, posz);
        }

        Game.BlockHealth[key] -= random.Next(2, 4);

        if (Game.GetCurrentBlockHealth(posx, posy, posz) <= 0)
        {
            Game.BlockHealth.Remove(key);
            Game.CurrentAttackedBlock = null;
            int brokenBlockType = voxelMap.GetBlock(posx, posy, posz);
            SpawnBlockBreakEffect(tile, brokenBlockType);
            OnPick(newtileX, posy, posz,
                (int)tile.Current()[0], (int)tile.Current()[2], (int)tile.Current()[1],
                tile.CollisionPos, right: false);

            // Force immediate upload — don't wait for next frame's capped flush
            _meshBatcher.FlushPendingUploads(int.MaxValue);
        }

        PickingEnd(left, right, middle, isPistol);
    }

    /// <summary>
    /// Handles middle-click: finds the pointed-at block type in the hotbar or
    /// inventory and selects or moves it to the active material slot.
    /// </summary>
    private void HandleMiddleClickClone(BlockPosSide pick0)
    {
        int newtileX = (int)pick0.Current()[0];
        int newtileY = (int)pick0.Current()[1];
        int newtileZ = (int)pick0.Current()[2];

        if (!voxelMap.IsValidPos(newtileX, newtileZ, newtileY))
        {
            return;
        }

        int cloneSource = voxelMap.GetBlock(newtileX, newtileZ, newtileY);
        int cloneSource2 = blockTypeRegistry.WhenPlayerPlacesGetsConvertedTo[cloneSource];

        // Search the hotbar first.
        bool found = false;
        for (int i = 0; i < 10; i++)
        {
            if (Game.Inventory.RightHand[i]?.InventoryItemType == InventoryItemType.Block
             && Game.Inventory.RightHand[i].BlockId == cloneSource2)
            {
                Game.ActiveMaterial = i;
                found = true;
            }
        }

        if (!found)
        {
            int freeHand = Game.InventoryUtil.FreeHand(Game.ActiveMaterial) ?? -1;

            for (int i = 0; i < Game.Inventory.Items.Length; i++)
            {
                Packet_PositionItem k = Game.Inventory.Items[i];
                if (k == null)
                {
                    continue;
                }

                if (k.Value_.InventoryItemType != InventoryItemType.Block || k.Value_.BlockId != cloneSource2)
                {
                    continue;
                }

                if (freeHand != -1)
                {
                    Game.WearItem(InventoryPositionMainArea(k.X, k.Y),
                                  InventoryPositionMaterialSelector(freeHand));
                    break;
                }

                if (Game.Inventory.RightHand[Game.ActiveMaterial]?.InventoryItemType == InventoryItemType.Block)
                {
                    Game.MoveToInventory(InventoryPositionMaterialSelector(Game.ActiveMaterial));
                    Game.WearItem(InventoryPositionMainArea(k.X, k.Y),
                                  InventoryPositionMaterialSelector(Game.ActiveMaterial));
                }
            }
        }

        string[] sound = blockTypeRegistry.CloneSound[cloneSource];
        if (sound != null)
        {
            Game.PlayAudio(sound[0]);
        } // TODO: sound cycle
    }

    /// <summary>
    /// Fires a single pistol bullet or grenade, performs entity hit detection,
    /// spawns bullet/grenade entities, decrements ammo, applies recoil, and
    /// triggers the next shot in a burst if required.
    /// </summary>
    private void FirePistol(Line3D pick,
        ArraySegment<BlockPosSide> pick2, int pick2count,
        InventoryItem item, bool isGrenade, float cookWait, ref int bulletsShot)
    {
        float toX = pick.End[0], toY = pick.End[1], toZ = pick.End[2];
        if (pick2count > 0)
        {
            toX = pick2[0].BlockPos[0];
            toY = pick2[0].BlockPos[1];
            toZ = pick2[0].BlockPos[2];
        }

        Packet_ClientShot shot = new()
        {
            FromX = EncodingHelper.EncodeFixedPoint(pick.Start[0]),
            FromY = EncodingHelper.EncodeFixedPoint(pick.Start[1]),
            FromZ = EncodingHelper.EncodeFixedPoint(pick.Start[2]),
            ToX = EncodingHelper.EncodeFixedPoint(toX),
            ToY = EncodingHelper.EncodeFixedPoint(toY),
            ToZ = EncodingHelper.EncodeFixedPoint(toZ),
            HitPlayer = -1
        };

        CheckEntityHitsForShot(pick, pick2, pick2count, isGrenade, ref shot);

        shot.WeaponBlock = item.BlockId;
        Game.LoadedAmmo[item.BlockId]--;
        Game.TotalAmmo[item.BlockId]--;

        float projectileSpeed = blockTypeRegistry.BlockTypes[item.BlockId].ProjectileSpeed;
        if (projectileSpeed == 0)
        {
            Game.EntityAddLocal(Entity.CreateBullet(pick.Start[0], pick.Start[1], pick.Start[2], toX, toY, toZ, 150));
        }
        else
        {
            SpawnGrenadeEntity(pick, item, toX, toY, toZ, projectileSpeed, cookWait, ref shot);
        }

        Game.SendPacketClient(new Packet_Client { Id = PacketType.Shot, Shot = shot });

        if (blockTypeRegistry.BlockTypes[item.BlockId].Sounds.ShootEnd.Length > 0)
        {
            Game.pistolcycle = random.Next() % blockTypeRegistry.BlockTypes[item.BlockId].Sounds.ShootEnd.Length;
            Game.PlayAudio(string.Format("{0}.ogg", blockTypeRegistry.BlockTypes[item.BlockId].Sounds.ShootEnd[Game.pistolcycle]));
        }

        // Apply recoil.
        Game.Player.Position.RotX -= random.Next() * Game.CurrentRecoil();
        Game.Player.Position.RotY += (random.Next() * Game.CurrentRecoil() * 2) - Game.CurrentRecoil();

        // Burst fire.
        bulletsShot++;
        if (bulletsShot < blockTypeRegistry.BlockTypes[item.BlockId].BulletsPerShot)
        {
            NextBullet(bulletsShot);
        }
    }

    /// <summary>
    /// Iterates all entities, checks the picking ray against their head and body
    /// boxes, and records any hit (preventing shots through terrain).
    /// Spawns a blood-splatter sprite for non-grenade hits.
    /// </summary>
    private void CheckEntityHitsForShot(Line3D pick,
        ArraySegment<BlockPosSide> pick2, int pick2count,
        bool isGrenade, ref Packet_ClientShot shot)
    {
        float eyeX = Game.Player.Position.X, eyeY = Game.Player.Position.Y, eyeZ = Game.Player.Position.Z;

        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Entity entity = Game.Entities[i];
            if (entity?.DrawModel == null || entity.NetworkPosition == null)
            {
                continue;
            }

            if (!entity.NetworkPosition.PositionLoaded)
            {
                continue;
            }

            float fx = entity.Position.X, fy = entity.Position.Y, fz = entity.Position.Z;
            float headSize = (entity.DrawModel.ModelHeight - entity.DrawModel.EyeHeight) * 2;
            float bodyH = entity.DrawModel.ModelHeight - headSize;
            const float r = 0.35f;

            Box3 bodyBox = new(new Vector3(fx - r, fy, fz - r), new Vector3(fx + r, fy + bodyH, fz + r));
            Box3 headBox = new(new Vector3(fx - r, fy + bodyH, fz - r), new Vector3(fx + r, fy + bodyH + headSize, fz + r));

            Vector3? hit = Intersection.CheckLineBoxExact(pick, headBox);
            bool isHead = hit != null;
            if (hit == null)
            {
                hit = Intersection.CheckLineBoxExact(pick, bodyBox);
            }

            if (hit == null)
            {
                continue;
            }

            // Do not allow shooting through terrain.
            bool blockedByTerrain = pick2count > 0
                && Vector3.Distance(new Vector3(pick2[0].BlockPos[0], pick2[0].BlockPos[1], pick2[0].BlockPos[2]), new Vector3(eyeX, eyeY, eyeZ))
                <= Vector3.Distance(new Vector3(hit.Value.X, hit.Value.Y, hit.Value.Z), new Vector3(eyeX, eyeY, eyeZ));
            if (blockedByTerrain)
            {
                continue;
            }

            if (!isGrenade)
            {
                Entity blood = new()
                {
                    Sprite = new Sprite { PositionX = hit.Value.X, PositionY = hit.Value.Y, PositionZ = hit.Value.Z, Image = "blood.png" },
                    Expires = Expiry.Create(0.2f)
                };
                Game.EntityAddLocal(blood);
            }

            shot.HitPlayer = i;
            shot.IsHitHead = isHead ? 1 : 0;
        }
    }

    /// <summary>
    /// Creates and spawns a grenade entity with the correct velocity, fuse time,
    /// and sprite, and writes the explosion timer into <paramref name="shot"/>.
    /// </summary>
    private void SpawnGrenadeEntity(Line3D pick, InventoryItem item,
        float toX, float toY, float toZ, float projectileSpeed, float cookWait,
        ref Packet_ClientShot shot)
    {
        float vX = toX - pick.Start[0];
        float vY = toY - pick.Start[1];
        float vZ = toZ - pick.Start[2];
        float len = new Vector3(vX, vY, vZ).Length;
        vX = vX / len * projectileSpeed;
        vY = vY / len * projectileSpeed;
        vZ = vZ / len * projectileSpeed;

        float fuseRemaining = Game.grenadetime - cookWait;
        shot.ExplodesAfter = EncodingHelper.EncodeFixedPoint(fuseRemaining);

        Entity grenadeEntity = new()
        {
            Sprite = new Sprite
            {
                Image = "ChemicalGreen.png",
                Size = 14,
                AnimationCount = 0,
                PositionX = pick.Start[0],
                PositionY = pick.Start[1],
                PositionZ = pick.Start[2]
            },
            Grenade = new Grenade
            {
                VelocityX = vX,
                VelocityY = vY,
                VelocityZ = vZ,
                Block = item.BlockId,
                SourcePlayer = Game.LocalPlayerId
            },
            Expires = Expiry.Create(fuseRemaining)
        };
        Game.EntityAddLocal(grenadeEntity);
    }

    /// <summary>
    /// Resets <see cref="fastclicking"/> and clears <see cref="lastbuildMilliseconds"/>
    /// when no mouse button is held and the held item is not a pistol.
    /// </summary>
    internal void PickingEnd(bool left, bool right, bool middle, bool isPistol)
    {
        fastclicking = false;
        if (!(left || right || middle) && !isPistol)
        {
            lastbuildMilliseconds = 0;
            fastclicking = true;
        }
    }

    /// <summary>
    /// Applies a block-set action at the picked position, taking into account
    /// rail direction snapping, the cuboid fill tool, and the fill-start marker.
    /// </summary>
    internal void OnPick(int blockposX, int blockposY, int blockposZ,
        int blockposOldX, int blockposOldY, int blockposOldZ,
        Vector3 collisionPos, bool right)
    {
        float xFract = collisionPos[0] - MathF.Floor(collisionPos[0]);
        float zFract = collisionPos[2] - MathF.Floor(collisionPos[2]);

        int activeMaterial = Game.MaterialSlots(Game.ActiveMaterial);
        int railStart = blockTypeRegistry.BlockIdRailStart;

        if (activeMaterial == railStart + (int)RailDirectionFlags.TwoHorizontalVertical
         || activeMaterial == railStart + (int)RailDirectionFlags.Corners)
        {
            RailDirection dirNew = activeMaterial == railStart + (int)RailDirectionFlags.TwoHorizontalVertical
                ? PickHorizontalVertical(xFract, zFract)
                : PickCorners(xFract, zFract);

            int dir = blockTypeRegistry.Rail[voxelMap.GetBlock(blockposOldX, blockposOldY, blockposOldZ)];
            if (dir != 0)
            {
                blockposX = blockposOldX;
                blockposY = blockposOldY;
                blockposZ = blockposOldZ;
            }

            activeMaterial = railStart + (dir | DirectionUtils.ToRailDirectionFlags(dirNew));
        }

        int x = blockposX;
        int y = blockposY;
        int z = blockposZ;
        PacketBlockSetMode mode = right ? PacketBlockSetMode.Create : PacketBlockSetMode.Destroy;

        if (Game.IsAnyPlayerInPos(x, y, z) || activeMaterial == 151 /* Compass */)
        {
            return;
        }

        Vector3i v = new(x, y, z);

        if (mode == PacketBlockSetMode.Create)
        {
            if (blockTypeRegistry.BlockTypes[activeMaterial].IsTool)
            {
                OnPickUseWithTool(blockposX, blockposY, blockposZ);
                return;
            }

            if (activeMaterial == blockTypeRegistry.BlockIdCuboid)
            {
                ClearFillArea();
                if (fillstart != null)
                {
                    Vector3i f = fillstart.Value;
                    if (!blockTypeRegistry.IsFillBlock(voxelMap.GetBlock(f.X, f.Y, f.Z)))
                    {
                        fillarea[(f.X, f.Y, f.Z)] = voxelMap.GetBlock(f.X, f.Y, f.Z);
                    }

                    Game.PlaceBlock(f.X, f.Y, f.Z, blockTypeRegistry.BlockIdFillStart);
                    FillFill(v, fillstart);
                }

                if (!blockTypeRegistry.IsFillBlock(voxelMap.GetBlock(v.X, v.Y, v.Z)))
                {
                    fillarea[(v.X, v.Y, v.Z)] = voxelMap.GetBlock(v.X, v.Y, v.Z);
                }

                Game.PlaceBlock(v.X, v.Y, v.Z, blockTypeRegistry.BlockIdCuboid);
                fillend = v;
                voxelMap.SetBlockDirty(v.X, v.Y, v.Z);
                return;
            }

            if (activeMaterial == blockTypeRegistry.BlockIdFillStart)
            {
                ClearFillArea();
                if (!blockTypeRegistry.IsFillBlock(voxelMap.GetBlock(v.X, v.Y, v.Z)))
                {
                    fillarea[(v.X, v.Y, v.Z)] = voxelMap.GetBlock(v.X, v.Y, v.Z);
                }

                Game.PlaceBlock(v.X, v.Y, v.Z, blockTypeRegistry.BlockIdFillStart);
                fillstart = v;
                fillend = null;
                voxelMap.SetBlockDirty(v.X, v.Y, v.Z);
                return;
            }

            if (fillarea.ContainsKey((v.X, v.Y, v.Z)))
            {
                Game.SendFillArea(fillstart.Value.X, fillstart.Value.Y, fillstart.Value.Z,
                                   fillend.Value.X, fillend.Value.Y, fillend.Value.Z,
                                   activeMaterial);
                ClearFillArea();
                fillstart = null;
                fillend = null;
                return;
            }
        }
        else
        {
            if (blockTypeRegistry.BlockTypes[activeMaterial].IsTool)
            {
                OnPickUseWithTool(blockposX, blockposY, blockposOldZ);
                return;
            }

            if (fillstart?.X == v.X && fillstart?.Y == v.Y && fillstart?.Z == v.Z)
            {
                ClearFillArea();
                fillstart = null;
                fillend = null;
                return;
            }

            if (fillend?.X == v.X && fillend?.Y == v.Y && fillend?.Z == v.Z)
            {
                ClearFillArea();
                fillend = null;
                return;
            }
        }

        Game.SendSetBlockAndUpdateSpeculative(activeMaterial, x, y, z, mode);
    }

    /// <summary>
    /// Spawns <paramref name="debrisCount"/> small debris sprites around
    /// (<paramref name="cx"/>, <paramref name="cy"/>, <paramref name="cz"/>).
    /// Each sprite is placed at a random offset within ±0.7 blocks and is
    /// given a randomised size (3–9 pixels) and lifetime (0.10–0.45 s).
    /// The slight positive Y bias means debris tends to appear above the
    /// block centre rather than below it, which reads more naturally when
    /// viewed from above.
    /// </summary>
    private void SpawnBlockBreakDebris(float cx, float cy, float cz, int debrisCount)
    {
        for (int i = 0; i < debrisCount; i++)
        {
            // Random offset — full XZ spread, half spread on Y with upward bias.
            float ox = (float)(random.NextDouble() - 0.5) * 1.4f;
            float oy = (float)(random.NextDouble() * 0.9 - 0.15f);
            float oz = (float)(random.NextDouble() - 0.5) * 1.4f;

            // Staggered lifetime: earliest pieces vanish after 100 ms,
            // latest after 450 ms — gives the cloud a natural fade.
            float life = 0.10f + (float)random.NextDouble() * 0.35f;

            // Randomised size so the debris cloud looks irregular rather
            // than a uniform grid of identically sized sprites.
            int size = 3 + random.Next(7);

            Game.EntityAddLocal(new Entity
            {
                Sprite = new Sprite
                {
                    PositionX = cx + ox,
                    PositionY = cy + oy,
                    PositionZ = cz + oz,
                    Image = "Gray.png",
                    Size = size
                },
                Expires = Expiry.Create(life)
            });
        }
    }

    /// <summary>
    /// Advances all active physics particles by one frame.
    /// Applies gravity, integrates position, detects ground contact,
    /// and applies bounce / friction on landing.
    /// Called from <see cref="OnNewFrameDraw3d"/> so it runs once per rendered frame.
    /// </summary>
    private void UpdateParticlePhysics(float dt)
    {
        for (int i = _physicsParticles.Count - 1; i >= 0; i--)
        {
            PhysicsParticle p = _physicsParticles[i];

            // ── Expire ───────────────────────────────────────────────────────
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                _physicsParticles.RemoveAt(i);
                continue;
            }

            // ── Physics (skipped once the particle has settled) ───────────────
            if (!p.Grounded)
            {
                // Gravity pulls in -Y (render-space convention: Y is up).
                p.VY -= ParticleGravity * dt;

                // Euler integration.
                p.Ent.Sprite.PositionX += p.VX * dt;
                p.Ent.Sprite.PositionY += p.VY * dt;
                p.Ent.Sprite.PositionZ += p.VZ * dt;

                // ── Ground detection ─────────────────────────────────────────
                // SampleGroundY reads the voxel map to find the actual terrain
                // surface, so particles land on the real block geometry rather
                // than an arbitrary flat plane.
                float groundY = SampleGroundY(
                    p.Ent.Sprite.PositionX,
                    p.Ent.Sprite.PositionY,
                    p.Ent.Sprite.PositionZ);

                if (p.Ent.Sprite.PositionY < groundY)
                {
                    p.Ent.Sprite.PositionY = groundY;

                    float bounceSpeed = MathF.Abs(p.VY) * ParticleBounce;

                    if (bounceSpeed > MinBounceVelocity)
                    {
                        // Reflect upward, lose energy, slow horizontal movement.
                        p.VY = bounceSpeed;
                        p.VX *= ParticleFriction;
                        p.VZ *= ParticleFriction;
                    }
                    else
                    {
                        // Not enough energy to bounce — rest on the surface.
                        // The particle will stay here until its lifetime expires.
                        p.VX = 0f;
                        p.VY = 0f;
                        p.VZ = 0f;
                        p.Grounded = true;
                    }
                }
            }

            _physicsParticles[i] = p;
        }
    }

    /// <summary>
    /// Returns the world-space render Y of the first solid block surface directly
    /// below the position (<paramref name="rx"/>, <paramref name="ry"/>, <paramref name="rz"/>).
    ///
    /// <para>
    /// <b>Coordinate mapping</b> (render → block space):
    /// <list type="bullet">
    ///   <item>render X → block X (unchanged)</item>
    ///   <item>render Y → block Z (height axis in block space)</item>
    ///   <item>render Z → block Y (second horizontal axis)</item>
    /// </list>
    /// <c>voxelMap.GetBlock(blockX, blockY, blockZ)</c> therefore becomes
    /// <c>voxelMap.GetBlock((int)rx, (int)rz, scanZ)</c> where <c>scanZ</c>
    /// steps downward from the particle's current block height.
    /// </para>
    ///
    /// <para>
    /// If particles clip through floors or float above surfaces, try swapping
    /// the second and third arguments to <c>voxelMap.GetBlock</c> — that is the
    /// only axis-mapping that may need adjustment across different build targets.
    /// </para>
    /// </summary>
    private float SampleGroundY(float rx, float ry, float rz)
    {
        int blockX = (int)MathF.Floor(rx);
        int blockY = (int)MathF.Floor(rz);      // render Z → block Y (horizontal)
        int startZ = (int)MathF.Floor(ry) - 1;  // start one block below particle

        // Clamp the start so we never scan above the world ceiling.
        startZ = Math.Min(startZ, 128);

        for (int blockZ = startZ; blockZ >= 0; blockZ--)
        {
            // Non-air block found — surface is at the top face of this block.
            // In render space the top face Y = blockZ + 1 (1:1 block scale).
            if (voxelMap.GetBlock(blockX, blockY, blockZ) != 0)
                return blockZ + 1f;
        }

        return 0f; // fell off the bottom of the map
    }

    /// <summary>
    /// Spawns the full Minecraft Bedrock-style block break effect at the broken block.
    ///
    /// <para>
    /// Three visual layers:
    /// <list type="number">
    ///   <item>
    ///     <b>Flash</b> — single large sprite at the block centre that vanishes in
    ///     ~60 ms, simulating the initial impact bloom.
    ///   </item>
    ///   <item>
    ///     <b>Physics particles</b> — <see cref="ParticleCount"/> small sprites that
    ///     launch outward with random velocities, arc under gravity, bounce once or
    ///     twice on landing, then rest on the terrain surface until their lifetime ends.
    ///     Positions are updated every frame by <see cref="UpdateParticlePhysics"/>.
    ///   </item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// Particle origin uses <c>tile.CollisionPos</c> (the exact ray/face intersection
    /// point) rather than the integer block centre, so the burst originates precisely
    /// at the face that was hit rather than always from the block's geometric centre.
    /// </para>
    /// </summary>
    private void SpawnBlockBreakEffect(BlockPosSide tile, int blockType)
    {
        // CollisionPos is in render/camera space (Y-up), same space used by
        // entity sprite positions and the picking ray.
        float cx = tile.CollisionPos.X;
        float cy = tile.CollisionPos.Y;
        float cz = tile.CollisionPos.Z;

        // ── Layer 1: impact flash ─────────────────────────────────────────────
        Game.EntityAddLocal(new Entity
        {
            Sprite = new Sprite
            {
                PositionX = cx,
                PositionY = cy,
                PositionZ = cz,
                Image = GetParticleTexture(blockType),   // replace with block-coloured texture later
                Size = 22
            },
            Expires = Expiry.Create(0.06f)
        });

        // ── Layer 2: physics particles ────────────────────────────────────────
        for (int i = 0; i < ParticleCount; i++)
        {
            // Random horizontal direction + upward bias.
            float angle = (float)(random.NextDouble() * Math.PI * 2.0);
            float hspeed = 2.0f + (float)random.NextDouble() * 4.0f;  // horizontal burst
            float vspeed = 4.0f + (float)random.NextDouble() * 5.5f;  // upward launch

            // Small random origin offset so the burst looks volumetric,
            // not like all particles shoot from one geometric point.
            float ox = (float)(random.NextDouble() - 0.5) * 0.45f;
            float oy = (float)(random.NextDouble() - 0.5) * 0.45f;
            float oz = (float)(random.NextDouble() - 0.5) * 0.45f;

            // Stagger lifetime so particles don't all disappear at once.
            float life = 1.2f + (float)random.NextDouble() * 1.4f;
            int size = 3 + random.Next(7);

            Entity e = new()
            {
                Sprite = new Sprite
                {
                    PositionX = cx + ox,
                    PositionY = cy + oy,
                    PositionZ = cz + oz,
                    Image = GetParticleTexture(blockType),
                    Size = size
                },
                Expires = Expiry.Create(life)
            };
            Game.EntityAddLocal(e);

            // Register for per-frame physics updates.
            _physicsParticles.Add(new PhysicsParticle
            {
                Ent = e,
                VX = MathF.Cos(angle) * hspeed,
                VY = vspeed,                      // Y is up in render space
                VZ = MathF.Sin(angle) * hspeed,
                Life = life,
                Grounded = false
            });
        }
    }

    private string GetParticleTexture(int blockType)
    {
        //if (_terrainTextureNames == null) return "blood.png";

        int[]? faceIds = _terrainChunkTesselator.TextureId?[blockType];
        if (faceIds == null || faceIds.Length == 0) return "blood.png";

        int atlasIndex = faceIds[0];
        if (atlasIndex < 0 ) return "blood.png";

        string name = blockTypeRegistry.BlockTypes[blockType].Name;
        return string.IsNullOrWhiteSpace(name) ? "blood.png" : $"{name.ToLower()}.png";
    }

    /// <summary>
    /// Restores all blocks overwritten by the fill-area tool to their original
    /// values and clears the fill-area dictionary.
    /// </summary>
    internal void ClearFillArea()
    {
        foreach (((int x, int y, int z), float value) in fillarea)
        {
            Game.PlaceBlock(x, y, z, (int)value);
            voxelMap.SetBlockDirty(x, y, z);
        }

        fillarea.Clear();
    }

    /// <summary>
    /// Fills the axis-aligned bounding box between <paramref name="a"/> and
    /// <paramref name="b"/> with fill-area marker blocks, recording each
    /// overwritten block so it can be restored by <see cref="ClearFillArea"/>.
    /// Aborts if the fill would exceed the game's fill-area limit.
    /// </summary>
    internal void FillFill(Vector3i a, Vector3i? b)
    {
        int startX = Math.Min(a.X, b.Value.X), endX = Math.Max(a.X, b.Value.X);
        int startY = Math.Min(a.Y, b.Value.Y), endY = Math.Max(a.Y, b.Value.Y);
        int startZ = Math.Min(a.Z, b.Value.Z), endZ = Math.Max(a.Z, b.Value.Z);

        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                for (int z = startZ; z <= endZ; z++)
                {
                    if (fillarea.Count > Game.FillAreaLimit)
                    {
                        ClearFillArea();
                        return;
                    }

                    if (!blockTypeRegistry.IsFillBlock(voxelMap.GetBlock(x, y, z)))
                    {
                        fillarea[(x, y, z)] = voxelMap.GetBlock(x, y, z);
                        Game.PlaceBlock(x, y, z, blockTypeRegistry.BlockIdFillArea);
                        voxelMap.SetBlockDirty(x, y, z);
                    }
                }
            }
        }
    }

    /// <summary>Sends a <c>UseWithTool</c> block-set packet for the block at the given position.</summary>
    internal void OnPickUseWithTool(int posX, int posY, int posZ)
        => Game.SendSetBlock(posX, posY, posZ, PacketBlockSetMode.UseWithTool,
                             Game.Inventory.RightHand[Game.ActiveMaterial].BlockId,
                             Game.ActiveMaterial);

    /// <summary>
    /// Determines the rail direction for a two-state (horizontal/vertical) rail
    /// based on the fractional hit position within the block face.
    /// </summary>
    internal static RailDirection PickHorizontalVertical(float xFract, float yFract)
    {
        if (yFract >= xFract && yFract >= 1 - xFract)
        {
            return RailDirection.Vertical;
        }

        if (yFract < xFract && yFract < 1 - xFract)
        {
            return RailDirection.Vertical;
        }

        return RailDirection.Horizontal;
    }

    /// <summary>
    /// Determines the corner rail direction based on which quadrant of the block
    /// face was hit.
    /// </summary>
    internal static RailDirection PickCorners(float xFract, float zFract)
    {
        if (xFract < 0.5f && zFract < 0.5f)
        {
            return RailDirection.UpLeft;
        }

        if (xFract >= 0.5f && zFract < 0.5f)
        {
            return RailDirection.UpRight;
        }

        if (xFract < 0.5f && zFract >= 0.5f)
        {
            return RailDirection.DownLeft;
        }

        return RailDirection.DownRight;
    }

    /// <summary>
    /// Performs entity-selection ray-casting for the interaction cursor
    /// (distinct from the shooting hit-detection in <see cref="CheckEntityHitsForShot"/>).
    /// Sets <c>game.SelectedEntityId</c> and <c>game.currentlyAttackedEntity</c>.
    /// </summary>
    private void PickEntity(Line3D pick,
        ArraySegment<BlockPosSide> pick2, int pick2count)
    {
        Game.SelectedEntityId = -1;
        Game.CurrentlyAttackedEntity = -1;

        float eyeX = Game.Player.Position.X, eyeY = Game.Player.Position.Y, eyeZ = Game.Player.Position.Z;

        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Entity entity = Game.Entities[i];
            if (entity?.DrawModel == null || i == Game.LocalPlayerId)
            {
                continue;
            }

            if (entity.NetworkPosition == null || !entity.NetworkPosition.PositionLoaded)
            {
                continue;
            }

            if (!entity.IsUsable)
            {
                continue;
            }

            float fx = entity.Position.X, fy = entity.Position.Y, fz = entity.Position.Z;
            if (Vector3.Distance(new Vector3(fx, fy, fz), new Vector3(Game.Player.Position.X, Game.Player.Position.Y, Game.Player.Position.Z)) > 5)
            {
                continue;
            }

            const float r = 0.35f;
            float h = entity.DrawModel.ModelHeight;
            Box3 bodyBox = new(new Vector3(fx - r, fy, fz - r), new Vector3(fx + r, fy + h, fz + r));

            Vector3? hit = Intersection.CheckLineBoxExact(pick, bodyBox);
            if (hit == null)
            {
                continue;
            }

            bool blockedByTerrain = pick2count > 0
                && Vector3.Distance(new Vector3(pick2[0].BlockPos[0] + 0.5f, pick2[0].BlockPos[1] + 0.5f, pick2[0].BlockPos[2] + 0.5f), new Vector3(eyeX, eyeY, eyeZ))
                <= Vector3.Distance(new Vector3(hit.Value.X, hit.Value.Y, hit.Value.Z), new Vector3(eyeX, eyeY, eyeZ));
            if (blockedByTerrain)
            {
                continue;
            }

            Game.SelectedEntityId = i;
            if (Game.CameraType is CameraType.Fpp or CameraType.Tpp)
            {
                Game.CurrentlyAttackedEntity = i;
            }
        }
    }

    /// <summary>
    /// When the player left-clicks and an entity is targeted, notifies all client
    /// mods and sends a hit packet to the server.
    /// </summary>
    private void UpdateEntityHit()
    {
        if (Game.CurrentlyAttackedEntity == -1 || !Game.mouseLeft)
        {
            return;
        }

        for (int i = 0; i < modRegistry.Mods.Count; i++)
        {
            if (modRegistry.Mods[i] == null)
            {
                continue;
            }

            modRegistry.Mods[i].OnHitEntity(new OnUseEntityArgs { Id = Game.CurrentlyAttackedEntity });
        }

        Game.SendPacketClient(ClientPackets.HitEntity(Game.CurrentlyAttackedEntity));
    }

    /// <summary>Placeholder called when the player picks a block in free-mouse mode.</summary>
    internal static void OnPick_(BlockPosSide pick0) { }

    /// <summary>
    /// Returns the minimum seconds between successive block actions for the
    /// currently held item. Derived from the item's <c>DelayFloat</c> field,
    /// or a movement-speed-scaled default when no delay is specified.
    /// </summary>
    internal float BuildDelay()
    {
        float defaultDelay = 0.95f / Game.Basemovespeed;
        InventoryItem item = Game.Inventory.RightHand[Game.ActiveMaterial];
        if (item == null || item.InventoryItemType != InventoryItemType.Block)
        {
            return defaultDelay;
        }

        float delay = blockTypeRegistry.BlockTypes[item.BlockId].Delay;
        return delay == 0 ? defaultDelay : delay;
    }

    /// <summary>
    /// Constructs the picking ray from the camera position through the screen
    /// centre (FPP/TPP) or mouse position (other camera types), optionally
    /// applying weapon spread for pistol shots.
    /// </summary>
    public void GetPickingLine(Line3D retPick, bool isPistolShoot)
    {
        int mouseX, mouseY;
        if (Game.CameraType is CameraType.Fpp or CameraType.Tpp)
        {
            mouseX = platform.CanvasWidth / 2;
            mouseY = platform.CanvasHeight / 2;
        }
        else
        {
            mouseX = Game.MouseCurrentX;
            mouseY = Game.MouseCurrentY;
        }

        PointF aim = GetAim();
        if (isPistolShoot && (aim.X != 0 || aim.Y != 0))
        {
            mouseX += (int)aim.X;
            mouseY += (int)aim.Y;
        }

        _tempViewport[0] = 0;
        _tempViewport[1] = 0;
        _tempViewport[2] = platform.CanvasWidth;
        _tempViewport[3] = platform.CanvasHeight;

        int flippedY = platform.CanvasHeight - mouseY;
        VectorUtils.UnProject(mouseX, flippedY, 1, meshDrawer.mvMatrix.Peek(), meshDrawer.pMatrix.Peek(), _tempViewport, out Vector3 rayEnd);
        VectorUtils.UnProject(mouseX, flippedY, 0, meshDrawer.mvMatrix.Peek(), meshDrawer.pMatrix.Peek(), _tempViewport, out Vector3 rayStart);

        float rdX = rayEnd.X - rayStart.X;
        float rdY = rayEnd.Y - rayStart.Y;
        float rdZ = rayEnd.Z - rayStart.Z;
        float len = new Vector3(rdX, rdY, rdZ).Length;
        rdX /= len;
        rdY /= len;
        rdZ /= len;

        float pickDist = (CurrentPickDistance() * (isPistolShoot ? 100 : 1)) + 1;

        retPick.Start = new Vector3(rayStart.X, rayStart.Y, rayStart.Z);
        retPick.End = new Vector3(rayStart.X + (rdX * pickDist),
                                    rayStart.Y + (rdY * pickDist),
                                    rayStart.Z + (rdZ * pickDist));
    }

    /// <summary>
    /// Returns a random offset within the weapon's aim circle for spread simulation.
    /// Returns <see cref="PointF.Empty"/> when the aim radius is 1 or less.
    /// </summary>
    internal PointF GetAim()
    {
        if (Game.CurrentAimRadius() <= 1)
        {
            return new PointF(0, 0);
        }

        float radius = Game.CurrentAimRadius();
        float x, y;
        do
        {
            x = (random.Next() - 0.5f) * radius * 2;
            y = (random.Next() - 0.5f) * radius * 2;
        }
        while (MathF.Sqrt((x * x) + (y * y)) > radius);

        return new PointF(x, y);
    }

    /// <summary>
    /// Returns the effective pick distance for the current camera type and held item.
    /// Overhead camera uses a fixed or distance-doubled range; TPP adds the camera
    /// offset; FPP uses the item's configured distance or the global default.
    /// </summary>
    private float CurrentPickDistance()
    {
        float distance = Game.PICK_DISTANCE;
        int? inHand = Game.BlockInHand();

        if (inHand.HasValue && blockTypeRegistry.BlockTypes[inHand.Value].PickDistanceWhenUsed > 0)
        {
            distance = blockTypeRegistry.BlockTypes[inHand.Value].PickDistanceWhenUsed;
        }

        if (Game.CameraType is CameraType.Tpp)
        {
            distance = Game.TppCameraDistance + Game.PICK_DISTANCE;
        }

        if (Game.CameraType is CameraType.Overhead)
        {
            distance = platform.IsFastSystem() ? 100 : cameraService.OverHeadCameraDistance * 2;
        }

        return distance;
    }

    internal static Packet_InventoryPosition InventoryPositionMaterialSelector(int materialId)
    {
        Packet_InventoryPosition pos = new()
        {
            Type = PacketInventoryPositionType.MaterialSelector,
            MaterialId = materialId
        };
        return pos;
    }

    internal static Packet_InventoryPosition InventoryPositionMainArea(int x, int y)
    {
        Packet_InventoryPosition pos = new()
        {
            Type = PacketInventoryPositionType.MainArea,
            AreaX = x,
            AreaY = y
        };
        return pos;
    }

}