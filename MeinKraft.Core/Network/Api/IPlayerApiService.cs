using System.Net.Http;
using System.Net.Http.Json;

public interface IPlayerApiService
{
    Task<bool> ExistsAsync(string username);
    Task<RegisterResponse?> RegisterAsync(string username);
}

public sealed class PlayerApiService : IPlayerApiService
{
    private readonly HttpClient _http;

    public PlayerApiService(HttpClient http) => _http = http;

    public async Task<bool> ExistsAsync(string username)
    {
        var response = await _http.GetFromJsonAsync<ExistsResponse>(
            $"/api/players/exists/{username}");
        return response?.Exists ?? false;
    }

    public async Task<RegisterResponse?> RegisterAsync(string username)
    {
        var result = await _http.PostAsJsonAsync(
            "/api/players/register",
            new { Username = username });

        result.EnsureSuccessStatusCode();
        return await result.Content.ReadFromJsonAsync<RegisterResponse>();
    }
}

public sealed record ExistsResponse(bool Exists);
public sealed record RegisterResponse(string Username, string ApiKey);