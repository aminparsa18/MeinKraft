/// <summary>
/// Clears draw info for players who have not sent a network update in over 2 seconds.
/// </summary>
public class ModClearInactivePlayersDrawInfo : ModBase
{
    private const float InactiveThresholdSeconds = 2f;

    private readonly IGame _game;
    private readonly IGameWindowService _platform;

    public ModClearInactivePlayersDrawInfo(IGameWindowService platform, IGame game) : base(game)
    {
        _platform = platform;
        _game = game;
    }

    public override void OnUpdate(float args)
    {
        if (true) //TODO: fix after multiplayer run
            return; //no need to clear draw info in single player mode

        int now = _platform.TimeMillisecondsFromStart;

        for (int i = 0; i < _game.Entities.Count; i++)
        {
            Entity p = _game.Entities[i];
            if (p?.PlayerDrawInfo == null || p.NetworkPosition == null)
            {
                continue;
            }

            float secondsSinceUpdate = (now - p.NetworkPosition.LastUpdateMilliseconds) / 1000f;
            if (secondsSinceUpdate > InactiveThresholdSeconds)
            {
                p.PlayerDrawInfo = null;
                p.NetworkPosition.PositionLoaded = false;
            }
        }
    }
}