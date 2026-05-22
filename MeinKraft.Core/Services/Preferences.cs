/// <summary>
/// File-backed implementation of <see cref="IPreferences"/>.
/// Values are persisted as <c>key=value</c> lines in <c>Preferences.txt</c>
/// inside the game's store path. All types are stored as strings and converted on read.
/// </summary>
public sealed class Preferences : IPreferences
{
    private readonly Dictionary<string, string> _items = [];

    /// <summary>
    /// Initialises the store and loads any previously saved preferences from disk.
    /// Missing or malformed lines are silently skipped.
    /// </summary>
    public Preferences()
    {
        string path = PreferencesFilePath();
        if (!File.Exists(path))
        {
            return;
        }

        foreach (string line in File.ReadAllLines(path))
        {
            int sep = line.IndexOf('=');
            if (sep <= 0)
            {
                continue; // skip blank keys and lines without '='
            }

            SetString(line[..sep], line[(sep + 1)..]);
        }
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public string GetString(string key, string default_)
        => _items.TryGetValue(key, out string? value) ? value : default_;

    /// <inheritdoc/>
    public bool GetBool(string key, bool default_)
        => GetString(key, null) switch
        {
            "0" => false,
            "1" => true,
            _ => default_,
        };

    /// <inheritdoc/>
    public int GetInt(string key, int default_)
    {
        string? raw = GetString(key, null);
        // Parsed via float to handle values stored with a decimal point gracefully.
        return raw is not null && float.TryParse(raw, out float result)
            ? (int)result
            : default_;
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void SetString(string key, string value) => _items[key] = value;

    /// <inheritdoc/>
    public void SetBool(string key, bool value) => SetString(key, value ? "1" : "0");

    /// <inheritdoc/>
    public void SetInt(string key, int value) => SetString(key, value.ToString());

    /// <inheritdoc/>
    public void Remove(string key) => _items.Remove(key);

    // ── Persistence ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void SetValues() => File.WriteAllLines(PreferencesFilePath(), ToLines());

    /// <inheritdoc/>
    public IEnumerable<string> ToLines()
        => _items.Select(kvp => $"{kvp.Key}={kvp.Value}");

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the absolute path to <c>Preferences.txt</c>, creating the
    /// store directory if it does not yet exist.
    /// </summary>
    private static string PreferencesFilePath()
    {
        string dir = GameStorePath.GetStorePath();
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return Path.Combine(dir, "Preferences.txt");
    }
}