public interface ISessionConfig
{
    string WorldName { get; set; }
    int Port { get; set; }
    string SavePath { get; set; }
}

public sealed class SessionConfig : ISessionConfig
{
    public string WorldName { get; set; } = string.Empty;
    public int Port { get; set; }
    public string SavePath { get; set; } = string.Empty;
}