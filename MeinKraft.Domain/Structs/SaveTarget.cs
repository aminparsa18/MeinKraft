
/// <summary>
/// Represents which file a session should load from or save to.
/// Constructed through the factory methods; never holds a raw path externally.
/// </summary>
public readonly record struct SaveTarget
{
    private readonly string? _path;

    private SaveTarget(string? path) => _path = path;

    /// <summary>No explicit file chosen — the service will use its default path.</summary>
    public static SaveTarget NewGame(string path) => new(path);

    /// <summary>Load from / save to a specific file the player chose.</summary>
    public static SaveTarget FromFile(string path) => new(path);

    public bool IsNewGame => _path is null;

    /// <summary>
    /// Resolves to <paramref name="defaultPath"/> when no explicit file was chosen.
    /// Internal — only <see cref="SaveGameService"/> should call this.
    /// </summary>
    public string Resolve(string defaultPath) => _path ?? defaultPath;
}