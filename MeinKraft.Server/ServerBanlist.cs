/// <summary>
/// Persisted ban list for the server. Holds username and IP bans, both permanent
/// and time-limited. Serialised to and from XML alongside the server config.
/// </summary>
public class ServerBanlist
{
    /// <summary>All currently banned usernames and their associated ban metadata.</summary>
    public List<UserEntry> BannedUsers { get; set; }

    /// <summary>All currently banned IP addresses and their associated ban metadata.</summary>
    public List<IPEntry> BannedIPs { get; set; }

    /// <summary>
    /// Initialises a new <see cref="ServerBanlist"/> with empty ban lists.
    /// </summary>
    public ServerBanlist()
    {
        BannedIPs = [];
        BannedUsers = [];
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="ipAddress"/> is currently
    /// on the IP ban list.
    /// </summary>
    public bool IsIPBanned(string ipAddress)
    {
        foreach (IPEntry bannedip in BannedIPs)
        {
            if (bannedip.IPAdress == ipAddress)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="username"/> is currently
    /// on the user ban list (case-insensitive).
    /// </summary>
    public bool IsUserBanned(string username)
    {
        foreach (UserEntry banneduser in BannedUsers)
        {
            if (username.Equals(banneduser.UserName, StringComparison.InvariantCultureIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Removes all time-limited bans whose expiry has passed.
    /// Permanent bans (<see cref="UserEntry.BannedUntil"/> / <see cref="IPEntry.BannedUntil"/>
    /// is <see langword="null"/>) are never removed by this method.
    /// </summary>
    /// <returns>The number of bans that were lifted.</returns>
    public int ClearTimeBans()
    {
        List<string> unbanUsers = [];
        List<string> unbanIPs = [];

        foreach (UserEntry banneduser in BannedUsers)
        {
            if (banneduser.BannedUntil < DateTime.UtcNow)
                unbanUsers.Add(banneduser.UserName);
        }

        foreach (IPEntry bannedip in BannedIPs)
        {
            if (bannedip.BannedUntil < DateTime.UtcNow)
                unbanIPs.Add(bannedip.IPAdress);
        }

        int counter = 0;

        foreach (string username in unbanUsers)
            if (UnbanPlayer(username)) counter++;

        foreach (string ip in unbanIPs)
            if (UnbanIP(ip)) counter++;

        if (counter > 0)
            Console.WriteLine("Removed {0} expired timebans.", counter);

        return counter;
    }

    /// <summary>
    /// Permanently bans <paramref name="username"/>. Returns <see langword="false"/>
    /// if the user is already banned.
    /// </summary>
    /// <param name="username">Username to ban.</param>
    /// <param name="bannedby">Name of the moderator or system issuing the ban.</param>
    /// <param name="reason">Optional reason shown to the banned player.</param>
    public bool BanPlayer(string username, string bannedby, string reason)
        => TimeBanPlayer(username, bannedby, reason, 0);

    /// <summary>
    /// Bans <paramref name="username"/> for <paramref name="intervalMinutes"/> minutes.
    /// Pass <c>0</c> for a permanent ban. Returns <see langword="false"/> if the
    /// user is already banned.
    /// </summary>
    /// <param name="username">Username to ban.</param>
    /// <param name="bannedby">Name of the moderator or system issuing the ban.</param>
    /// <param name="reason">Optional reason shown to the banned player.</param>
    /// <param name="intervalMinutes">
    /// Duration of the ban in minutes. <c>0</c> means permanent.
    /// </param>
    public bool TimeBanPlayer(string username, string bannedby, string reason, int intervalMinutes)
    {
        if (IsUserBanned(username))
            return false;

        UserEntry newBan = new() { UserName = username, BannedBy = bannedby };

        if (intervalMinutes > 0)
            newBan.BannedUntil = DateTime.UtcNow + TimeSpan.FromMinutes(intervalMinutes);

        if (!string.IsNullOrEmpty(reason))
            newBan.Reason = reason;

        BannedUsers.Add(newBan);
        return true;
    }

    /// <summary>
    /// Permanently bans <paramref name="ipAddress"/>. Returns <see langword="false"/>
    /// if the address is already banned.
    /// </summary>
    /// <param name="ipAddress">IP address to ban.</param>
    /// <param name="bannedby">Name of the moderator or system issuing the ban.</param>
    /// <param name="reason">Optional reason recorded in the ban entry.</param>
    public bool BanIP(string ipAddress, string bannedby, string reason)
        => TimeBanIP(ipAddress, bannedby, reason, 0);

    /// <summary>
    /// Bans <paramref name="ipAddress"/> for <paramref name="intervalMinutes"/> minutes.
    /// Pass <c>0</c> for a permanent ban. Returns <see langword="false"/> if the
    /// address is already banned.
    /// </summary>
    /// <param name="ipAddress">IP address to ban.</param>
    /// <param name="bannedby">Name of the moderator or system issuing the ban.</param>
    /// <param name="reason">Optional reason recorded in the ban entry.</param>
    /// <param name="intervalMinutes">
    /// Duration of the ban in minutes. <c>0</c> means permanent.
    /// </param>
    public bool TimeBanIP(string ipAddress, string bannedby, string reason, int intervalMinutes)
    {
        if (IsIPBanned(ipAddress))
            return false;

        IPEntry newBan = new() { IPAdress = ipAddress, BannedBy = bannedby };

        if (intervalMinutes > 0)
            newBan.BannedUntil = DateTime.UtcNow + TimeSpan.FromMinutes(intervalMinutes);

        if (!string.IsNullOrEmpty(reason))
            newBan.Reason = reason;

        BannedIPs.Add(newBan);
        return true;
    }

    /// <summary>
    /// Removes the ban for <paramref name="username"/> (case-insensitive).
    /// Returns <see langword="false"/> if no matching ban was found.
    /// </summary>
    public bool UnbanPlayer(string username)
    {
        for (int i = BannedUsers.Count - 1; i >= 0; i--)
        {
            if (BannedUsers[i].UserName.Equals(username, StringComparison.InvariantCultureIgnoreCase))
            {
                BannedUsers.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes the ban for <paramref name="ip"/> (case-insensitive).
    /// Returns <see langword="false"/> if no matching ban was found.
    /// </summary>
    public bool UnbanIP(string ip)
    {
        for (int i = BannedIPs.Count - 1; i >= 0; i--)
        {
            if (BannedIPs[i].IPAdress.Equals(ip, StringComparison.InvariantCultureIgnoreCase))
            {
                BannedIPs.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the <see cref="UserEntry"/> for <paramref name="username"/>, or
    /// <see langword="null"/> if the user is not banned.
    /// </summary>
    public UserEntry GetUserEntry(string username)
    {
        foreach (UserEntry banneduser in BannedUsers)
        {
            if (username.Equals(banneduser.UserName, StringComparison.InvariantCultureIgnoreCase))
                return banneduser;
        }

        return null;
    }

    /// <summary>
    /// Returns the <see cref="IPEntry"/> for <paramref name="ipAddress"/>, or
    /// <see langword="null"/> if the address is not banned.
    /// </summary>
    public IPEntry GetIPEntry(string ipAddress)
    {
        foreach (IPEntry bannedip in BannedIPs)
        {
            if (bannedip.IPAdress == ipAddress)
                return bannedip;
        }

        return null;
    }
}

/// <summary>
/// A single username ban entry, optionally time-limited.
/// </summary>
public class UserEntry
{
    /// <summary>The banned player's username.</summary>
    public string UserName { get; set; }

    /// <summary>Name of the moderator or system that issued the ban.</summary>
    public string BannedBy { get; set; }

    /// <summary>
    /// UTC expiry time for a time-limited ban. <see langword="null"/> means the
    /// ban is permanent and will not be lifted by <see cref="ServerBanlist.ClearTimeBans"/>.
    /// </summary>
    public DateTime? BannedUntil { get; set; }

    /// <summary>Optional human-readable reason for the ban.</summary>
    public string Reason { get; set; }
}

/// <summary>
/// A single IP address ban entry, optionally time-limited.
/// </summary>
public class IPEntry
{
    /// <summary>The banned IP address.</summary>
    public string IPAdress { get; set; }

    /// <summary>Name of the moderator or system that issued the ban.</summary>
    public string BannedBy { get; set; }

    /// <summary>
    /// UTC expiry time for a time-limited ban. <see langword="null"/> means the
    /// ban is permanent and will not be lifted by <see cref="ServerBanlist.ClearTimeBans"/>.
    /// </summary>
    public DateTime? BannedUntil { get; set; }

    /// <summary>Optional human-readable reason for the ban.</summary>
    public string Reason { get; set; }
}