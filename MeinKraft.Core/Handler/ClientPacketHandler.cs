/// <summary>
/// Base class for all server-packet handlers.
/// Implementations receive a fully-deserialized <see cref="Packet_Server"/> and
/// are responsible for applying its effect to the game state.
/// </summary>
public abstract class ClientPacketHandler(IGameWindowService gameService, IGame game)
{
    protected readonly IGame game = game;
    protected readonly IGameWindowService gameService = gameService;

    /// <summary>Applies the effect of <paramref name="packet"/> to <paramref name="game"/>.</summary>
    public abstract void Handle(Packet_Server packet);
}