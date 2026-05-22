public static class GameStorePath
{
    public static bool IsMono = Type.GetType("Mono.Runtime") != null;

    public static string GetStorePath()
    {
        string apppath = Path.GetDirectoryName(AppContext.BaseDirectory);
        DirectoryInfo di = new(apppath);
        if (di.Name.Equals("AutoUpdaterTemp", StringComparison.InvariantCultureIgnoreCase))
        {
            apppath = di.Parent.FullName;
        }

        string mdfolder = "UserData";
        if (apppath.Contains(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)) && !IsMono)
        {
            string mdpath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                mdfolder);
            return mdpath;
        }
        else
        {
            return Path.Combine(apppath, mdfolder);
        }
    }

    public static string gamepathconfig = Path.Combine(GetStorePath(), "Configuration");
    public static string gamepathsaves = Path.Combine(GetStorePath(), "Saves");
    public static string gamepathbackup = Path.Combine(GetStorePath(), "Backup");

    public static string WorldSavePath(string playerId, string worldName)
        => Path.Combine(gamepathsaves, playerId, worldName + FileConstatns.DbFileExtension);

    public static bool IsValidName(string s)
    {
        if (s.Length is < 1 or > 32)
        {
            return false;
        }

        for (int i = 0; i < s.Length; i++)
        {
            if (!AllowedNameChars.Contains(s[i].ToString()))
            {
                return false;
            }
        }

        return true;
    }
    public static string AllowedNameChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890_-";
}
