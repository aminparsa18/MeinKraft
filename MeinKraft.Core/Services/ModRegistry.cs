/// <summary>
/// Late-bound collection of mods populated after the DI container is built.
/// Breaks the circular dependency between <see cref="IGame"/> and <see cref="IModBase"/>.
/// </summary>
public interface IModRegistry
{
    IReadOnlyList<IModBase> Mods { get; }
    void Initialise(IEnumerable<IModBase> mods);
}

public sealed class ModRegistry : IModRegistry
{
    public IReadOnlyList<IModBase> Mods { get; private set; } = [];

    public void Initialise(IEnumerable<IModBase> mods)
    {
        if (Mods.Count > 0)
        {
            throw new InvalidOperationException("ModRegistry already initialised.");
        }

        Mods = [.. mods];
    }
}