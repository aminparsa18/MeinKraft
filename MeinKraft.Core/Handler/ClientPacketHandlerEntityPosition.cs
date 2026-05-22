/// <summary>
/// Handles <see cref="Packet_ServerIdEnum.EntityPosition"/> packets,
/// updating networked entity positions and orientations each time the server
/// sends a movement update.
/// </summary>
public class ClientPacketHandlerEntityPosition(IGameWindowService gameService, IGame game) : ClientPacketHandler(gameService, game)
{
    public override void Handle(Packet_Server packet)
    {
        int id = packet.EntityPosition.Id;
        Entity entity = game.Entities[id];
        Packet_PositionAndOrientation raw = packet.EntityPosition.PositionAndOrientation;

        if (id == game.LocalPlayerId)
        {
            // Local player: apply as an authoritative teleport directly onto
            // player.position. No EntityPosition_ object is needed here.
            game.LocalPositionX = raw.X / 32f;
            game.LocalPositionY = raw.Y / 32f;
            game.LocalPositionZ = raw.Z / 32f;
            game.LocalOrientationX = ClientPacketHandlerEntitySpawn.Angle256ToRad(raw.Pitch);
            game.LocalOrientationY = ClientPacketHandlerEntitySpawn.Angle256ToRad(raw.Heading);
            entity.NetworkPosition = null;
        }
        else if (entity.Push != null)
        {
            // Entity has a push force — forward raw fixed-point coords directly.
            entity.Push.XFloat = raw.X;
            entity.Push.YFloat = raw.Z;
            entity.Push.ZFloat = raw.Y;
        }
        else
        {
            // ── Update networkPosition in-place ───────────────────────────────
            // Previously called ToClientEntityPosition() which allocated a new
            // EntityPosition_ object on every packet. Position updates arrive at
            // network rate for every visible entity — this was the hottest
            // allocation in the entity system.
            // We allocate once (lazily) and overwrite fields on every update.
            entity.NetworkPosition ??= new EntityPosition();
            entity.NetworkPosition.X = raw.X / 32f;
            entity.NetworkPosition.Y = raw.Y / 32f;
            entity.NetworkPosition.Z = raw.Z / 32f;
            entity.NetworkPosition.RotX = ClientPacketHandlerEntitySpawn.Angle256ToRad(raw.Pitch);
            entity.NetworkPosition.RotY = ClientPacketHandlerEntitySpawn.Angle256ToRad(raw.Heading);
            entity.NetworkPosition.PositionLoaded = true;
            entity.NetworkPosition.LastUpdateMilliseconds = gameService.TimeMillisecondsFromStart;
        }
    }
}