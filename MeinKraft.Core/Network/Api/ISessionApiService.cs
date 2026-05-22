using System.Net.Http;
using System.Net.Http.Json;

public interface ISessionApiService
{
    Task<StartSessionResponse?> StartAsync(string worldName);
    Task StopAsync(Guid sessionId);
    Task<IReadOnlyList<SessionInfo>> ListAsync();
}

public sealed class SessionApiService : ISessionApiService
{
    private readonly HttpClient _http;

    public SessionApiService(HttpClient http) => _http = http;

    public async Task<StartSessionResponse?> StartAsync(string worldName)
    {
        var result = await _http.PostAsJsonAsync(
            "/api/sessions/start",
            new { WorldName = worldName });

        result.EnsureSuccessStatusCode();
        return await result.Content.ReadFromJsonAsync<StartSessionResponse>();
    }

    public async Task StopAsync(Guid sessionId)
    {
        var result = await _http.DeleteAsync($"/api/sessions/{sessionId}");
        result.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<SessionInfo>> ListAsync()
    {
        return await _http.GetFromJsonAsync<List<SessionInfo>>("/api/sessions/")
               ?? [];
    }
}

public sealed record StartSessionResponse(Guid SessionId, int Port);
public sealed record SessionInfo(Guid Id, string WorldName, int Port);