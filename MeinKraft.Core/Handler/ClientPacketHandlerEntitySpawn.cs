using MessagePipe;

/// <summary>
/// Handles <see cref="Packet_ServerIdEnum.EntitySpawn"/> packets,
/// creating or updating entities and bootstrapping the local player on first spawn.
/// </summary>
public class ClientPacketHandlerEntitySpawn : ClientPacketHandler
{
    private readonly IVoxelMap voxelMap;
    private readonly IBlockRegistry blockTypeRegistry;
    private readonly IPublisher<SetupProgressEventArgs> _publisher;

    public ClientPacketHandlerEntitySpawn(IGameWindowService gameService, IVoxelMap voxelMap, IBlockRegistry blockTypeRegistry,
        IPublisher<SetupProgressEventArgs> publisher, IGame game) : base(gameService, game)
    {
        this.voxelMap = voxelMap;
        this.blockTypeRegistry = blockTypeRegistry;
        _publisher = publisher;
    }

    public override void Handle(Packet_Server packet)
    {
        int id = packet.EntitySpawn.Id;
        Entity entity = game.Entities[id] ?? new Entity();

        ToClientEntity(packet.EntitySpawn.Entity_, entity,
            updatePosition: id != game.LocalPlayerId);

        game.Entities[id] = entity;

        if (id == game.LocalPlayerId)
        {
            entity.NetworkPosition = null;
            game.Player = entity;
            if (!game.Spawned)
            {
                game.Spawned = true;
                _ = SpawnAfterDelayAsync(entity);
            }
        }
    }

    private readonly SynchronizationContext? _mainContext = SynchronizationContext.Current;

    private async Task SpawnAfterDelayAsync(Entity entity)
    {
        await Task.Delay(TimeSpan.FromSeconds(3));

        _mainContext?.Post(_ =>
        {
            entity.Scripts.Add(new ScriptCharacterPhysics(voxelMap, blockTypeRegistry, game));
            game.MapLoaded();
            _publisher.Publish(new SetupProgressEventArgs() { Progress = 100, Title = "All Done!" });
        }, null);
    }

    /// <summary>Converts a heading/pitch encoded as 0–255 to radians.</summary>
    internal static float Angle256ToRad(int value) => value / 255f * MathF.PI * 2;

    /// <summary>
    /// Decodes a fixed-point <see cref="Packet_PositionAndOrientation"/> into an
    /// <see cref="EntityPosition"/>. Used on the spawn path (cold) and by
    /// <see cref="ToClientEntity"/> when initialising a newly arrived entity.
    /// Position-update packets use in-place field mutation instead — see
    /// <see cref="ClientPacketHandlerEntityPosition"/>.
    /// </summary>
    public static EntityPosition ToClientEntityPosition(Packet_PositionAndOrientation pos)
    {
        return new EntityPosition
        {
            X = pos.X / 32f,
            Y = pos.Y / 32f,
            Z = pos.Z / 32f,
            RotX = Angle256ToRad(pos.Pitch),
            RotY = Angle256ToRad(pos.Heading),
        };
    }

    /// <summary>
    /// Applies all fields of a server entity descriptor onto an existing
    /// <paramref name="old"/> entity object, allocating sub-objects only when
    /// the corresponding server field is present.
    /// </summary>
    public Entity ToClientEntity(Packet_ServerEntity entity, Entity old, bool updatePosition)
    {
        if (entity.Position != null && (old.Position == null || updatePosition))
        {
            old.NetworkPosition = ToClientEntityPosition(entity.Position);
            old.NetworkPosition.PositionLoaded = true;
            old.NetworkPosition.LastUpdateMilliseconds = gameService.TimeMillisecondsFromStart;
            old.Position = ToClientEntityPosition(entity.Position);
        }

        if (entity.DrawModel != null)
        {
            old.DrawModel = new EntityDrawModel
            {
                EyeHeight = EncodingHelper.DecodeFixedPoint(entity.DrawModel.EyeHeight),
                ModelHeight = EncodingHelper.DecodeFixedPoint(entity.DrawModel.ModelHeight),
                Texture_ = entity.DrawModel.Texture_,
                Model = entity.DrawModel.Model_ ?? "player.txt",
                DownloadSkin = entity.DrawModel.DownloadSkin != 0,
            };
        }

        if (entity.DrawName_ != null)
        {
            string name = entity.DrawName_.Color != null
                ? string.Format("{0}{1}", entity.DrawName_.Color, entity.DrawName_.Name)
                : entity.DrawName_.Name;

            if (!name.StartsWith("&", StringComparison.InvariantCultureIgnoreCase))
            {
                name = string.Format("&f{0}", name);
            }

            old.DrawName = new DrawName
            {
                Name = name,
                OnlyWhenSelected = entity.DrawName_.OnlyWhenSelected,
                ClientAutoComplete = entity.DrawName_.ClientAutoComplete,
            };
        }

        if (entity.DrawText != null)
        {
            old.DrawText = new EntityDrawText
            {
                Text = entity.DrawText.Text,
                X = entity.DrawText.Dx / 32f,
                Y = entity.DrawText.Dy / 32f,
                Z = entity.DrawText.Dz / 32f,
            };
        }
        else
        {
            old.DrawText = null;
        }

        old.Push = entity.Push != null
            ? new Packet_ServerExplosion { RangeFloat = entity.Push.RangeFloat }
            : null;

        old.IsUsable = entity.Usable;

        old.DrawArea = entity.DrawArea != null
            ? new EntityDrawArea
            {
                X = entity.DrawArea.X,
                Y = entity.DrawArea.Y,
                Z = entity.DrawArea.Z,
                SizeX = entity.DrawArea.Sizex,
                SizeY = entity.DrawArea.Sizey,
                SizeZ = entity.DrawArea.Sizez,
            }
            : null;

        return old;
    }
}