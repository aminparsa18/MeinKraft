using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class PlayerRegistry
{
    private readonly string _filePath;
    private readonly List<PlayerRecord> _players;

    public PlayerRegistry(IWebHostEnvironment env)
    {
        _filePath = Path.Combine(env.ContentRootPath, "players.json");

        if (!File.Exists(_filePath))
        {
            File.WriteAllText(_filePath, "{\"players\":[]}");
        }

        var json = File.ReadAllText(_filePath);
        var doc = JsonSerializer.Deserialize<PlayersFile>(json);
        _players = doc?.Players ?? [];
    }

    public PlayerRecord? Authenticate(string apiKey)
        => _players.FirstOrDefault(p => p.ApiKey == apiKey);

    public PlayerRecord? GetByUsername(string username)
        => _players.FirstOrDefault(p => p.Username == username);

    public PlayerRecord Register(string username)
    {
        var player = new PlayerRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = username,
            ApiKey = Guid.NewGuid().ToString("N"),
        };

        _players.Add(player);
        Save();
        return player;
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(
            new PlayersFile { Players = _players },
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}

public sealed class PlayersFile
{
    [JsonPropertyName("players")]
    public List<PlayerRecord> Players { get; set; } = [];
}

public sealed class PlayerRecord
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}