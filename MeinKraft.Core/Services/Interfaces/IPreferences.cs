/// <summary>
/// A string-keyed settings store supporting bool, int, and string values.
/// Implementations are responsible for persistence — call <see cref="SetValues"/>
/// to flush in-memory state to the backing store.
/// </summary>
public interface IPreferences
{
    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the string value stored under <paramref name="key"/>,
    /// or <paramref name="default_"/> if the key is absent.
    /// </summary>
    string GetString(string key, string default_);

    /// <summary>
    /// Returns the bool value stored under <paramref name="key"/>,
    /// or <paramref name="default_"/> if the key is absent or not a recognised bool token.
    /// </summary>
    bool GetBool(string key, bool default_);

    /// <summary>
    /// Returns the int value stored under <paramref name="key"/>,
    /// or <paramref name="default_"/> if the key is absent or cannot be parsed.
    /// </summary>
    int GetInt(string key, int default_);

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/>.</summary>
    void SetString(string key, string value);

    /// <summary>
    /// Stores <paramref name="value"/> under <paramref name="key"/> as
    /// <c>"1"</c> (true) or <c>"0"</c> (false).
    /// </summary>
    void SetBool(string key, bool value);

    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/> as its string representation.</summary>
    void SetInt(string key, int value);

    /// <summary>Removes the entry for <paramref name="key"/> if it exists.</summary>
    void Remove(string key);

    // ── Persistence ───────────────────────────────────────────────────────────

    /// <summary>
    /// Flushes all in-memory key-value pairs to the backing store.
    /// Must be called explicitly — no auto-save occurs on individual set operations.
    /// </summary>
    void SetValues();

    /// <summary>
    /// Returns the current contents as a sequence of <c>key=value</c> lines,
    /// suitable for serialisation or display.
    /// </summary>
    IEnumerable<string> ToLines();
}