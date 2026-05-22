/// <summary>
/// Represents a single server entry returned from the server list.
/// </summary>
public class ServerOnList
{
    public required string Hash { get; set; }
    public required string Name { get; set; }
    public required string Motd { get; set; }
    public int Port { get; set; }
    public required string Ip { get; set; }
    public required string Version { get; set; }
    public int Users { get; set; }
    public int Max { get; set; }
    public required string GameMode { get; set; }
    public required string Players { get; set; }
    public bool ThumbnailDownloading { get; set; }
    public bool ThumbnailError { get; set; }
    public bool ThumbnailFetched { get; set; }
}