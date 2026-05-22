namespace MeinKraft.Worker;

/// <summary>
/// Contract for the fixed-timestep simulation tick.
/// The server mods, physics, and network drain all live behind this interface.
/// </summary>
public interface ISimulationStep
{
    /// <summary>
    /// Advance the world by one simulation tick.
    /// <paramref name="dt"/> is the wall-clock time since the previous call,
    /// in seconds. The implementation is responsible for its own accumulator
    /// if it needs a truly fixed internal step.
    /// </summary>
    void Tick(float dt);
}