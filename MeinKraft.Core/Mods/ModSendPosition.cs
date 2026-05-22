/// <summary>
/// Sends the local player's position and orientation to the server at most every 100ms.
/// </summary>
public class ModSendPosition : ModBase
{
    private const int SendIntervalMs = 100;
    private readonly IGameWindowService platform;

    public ModSendPosition(IGameWindowService platform, IGame game) : base(game)
    {
        this.platform = platform;
    }

    public override void OnFrame(float dt)
    {
        if (!Game.Spawned || Game.IsSinglePlayer)
            return;

        if (platform.TimeMillisecondsFromStart - Game.LastPositionSentMilliseconds <= SendIntervalMs)
            return;

        Game.LastPositionSentMilliseconds = platform.TimeMillisecondsFromStart;

        EntityPosition pos = Game.Player.Position;
        Game.SendPacketClient(ClientPackets.PositionAndOrientation(
            Game.LocalPlayerId,
            pos.X, pos.Y, pos.Z,
            pos.RotX, pos.RotY, pos.RotZ,
            Game.LocalStance));
    }
}