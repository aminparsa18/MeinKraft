public static class PathHelper
{
    /// <summary>
    /// Finds the game's data root directory, working correctly for both
    /// WinForms (bin\Debug\net10.0-windows\) and MAUI output layouts.
    /// </summary>
    public static string DataRoot { get; } = FindFolder("data");
    public static string ModsRoot { get; } = FindFolder(Path.Combine("MeinKraft.Core", "Server", "Mods"));

    private static string FindFolder(string folderRelativeName)
    {
        // Fast path: right next to the exe (published/MAUI builds)
        string next = Path.Combine(AppContext.BaseDirectory, folderRelativeName);
        if (Directory.Exists(next))
        {
            return next;
        }

        // Walk up from exe until we find it (WinForms dev builds)
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory).Parent;
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, folderRelativeName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        // Fallback
        return next;
    }
}