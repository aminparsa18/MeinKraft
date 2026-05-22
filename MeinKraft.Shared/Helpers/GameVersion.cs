public static class GameVersion
{
    public static string Version
    {
        get
        {
            if (field == null)
            {
                field = "unknown";
                if (File.Exists("version.txt"))
                {
                    field = File.ReadAllText("version.txt").Trim();
                }
            }

            return field;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if the server's version is at or after the given date.
    /// A <see langword="null"/> version is treated as up-to-date (returns <see langword="true"/>).
    /// </summary>
    /// <param name="serverGameVersion">The server version string, expected in <c>yyyy.MM.dd</c> format.</param>
    /// <param name="year">Minimum required year.</param>
    /// <param name="month">Minimum required month.</param>
    /// <param name="day">Minimum required day.</param>
    public static bool ServerVersionAtLeast(string serverGameVersion, int year, int month, int day)
        => serverGameVersion == null || VersionToInt(serverGameVersion) >= DateToInt(year, month, day);

    /// <summary>
    /// Returns <see langword="true"/> if the version string begins with a date in <c>yyyy-MM-dd</c> format.
    /// </summary>
    private static bool IsVersionDate(string version)
        => version.Length >= 10 && version[4] == '-' && version[7] == '-';

    /// <summary>
    /// Converts a date-based version string to a comparable integer (<c>yyyyMMdd</c>).
    /// Returns <c>1,000,000,000</c> if the version is not a recognised date format,
    /// treating it as newer than any date-versioned release.
    /// </summary>
    private static int VersionToInt(string version)
    {
        const int max = 1_000_000_000;
        return !IsVersionDate(version)
            ? max
            : DateTime.TryParseExact(
            version[..10],
            "yyyy.MM.dd",
            null,
            System.Globalization.DateTimeStyles.None,
            out DateTime date)
            ? DateToInt(date.Year, date.Month, date.Day)
            : max;
    }

    /// <summary>Encodes a date as a comparable integer in the form <c>yyyyMMdd</c>.</summary>
    private static int DateToInt(int year, int month, int day)
        => (year * 10000) + (month * 100) + day;
}