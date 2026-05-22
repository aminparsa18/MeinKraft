using System.Net.Http;
using System.Net.Http.Json;

public sealed record WorldInfo(string Name, DateTime LastPlayed)
{
    public string LastPlayedText => LastPlayed == DateTime.MinValue
        ? "never played"
        : $"last played  {LastPlayed:MMM dd yyyy}";
}

public sealed record CreateWorldRequest(string Name);

public interface IWorldApiService
{
    Task<IReadOnlyList<WorldInfo>> ListAsync();
    Task<WorldInfo?> CreateAsync(string worldName);
    Task DeleteAsync(string worldName);
}

public sealed class WorldApiService : IWorldApiService
{
    private readonly HttpClient _http;

    public WorldApiService(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<WorldInfo>> ListAsync()
        => await _http.GetFromJsonAsync<List<WorldInfo>>("/api/worlds") ?? [];

    public async Task<WorldInfo?> CreateAsync(string worldName)
    {
        var result = await _http.PostAsJsonAsync("/api/worlds", new CreateWorldRequest(worldName));
        result.EnsureSuccessStatusCode();
        var ss = await result.Content.ReadAsStringAsync();
        return await result.Content.ReadFromJsonAsync<WorldInfo>();
    }

    public async Task DeleteAsync(string worldName)
    {
        var result = await _http.DeleteAsync($"/api/worlds/{worldName}");
        result.EnsureSuccessStatusCode();
    }
}