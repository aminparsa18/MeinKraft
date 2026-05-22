/// <summary>Holds metadata about a discovered or saved server entry.</summary>
public class ServerInformation
{
    /// <summary>The display name of the server.</summary>
    public string ServerName { get; set; } = string.Empty;

    /// <summary>The server's message of the day.</summary>
    public string ServerMotd { get; set; } = string.Empty;

    /// <summary>Connection parameters used to join this server.</summary>
    public ConnectionData ConnectData { get; set; } = new();

    /// <summary>Ping instance used to measure latency to this server.</summary>
    public Ping ServerPing { get; set; } = new();
}