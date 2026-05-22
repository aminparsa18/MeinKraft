public static class GameSessionEndpoints
{
    public static IEndpointRouteBuilder MapGameSessionEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/sessions");

        group.MapPost("/start", async (
            StartSessionRequest req,
            IGameSessionManager sessions) =>
        {
            Guid id = await sessions.StartSessionAsync(req.WorldName);
            GameSession session = sessions.ActiveSessions[id];
            return Results.Ok(new { sessionId = id, port = session.Port });
        });

        group.MapDelete("/{sessionId}", async (
            Guid sessionId,
            IGameSessionManager sessions) =>
        {
            await sessions.StopSessionAsync(sessionId);
            return Results.NoContent();
        });

        group.MapGet("/", (IGameSessionManager sessions) =>
            Results.Ok(sessions.ActiveSessions.Values.Select(s => new
            {
                s.Id,
                s.WorldName,
                s.Port,
            })));

        group.MapPost("/register", (RegisterRequest req, PlayerRegistry registry) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username))
            {
                return Results.BadRequest("Username is required.");
            }

            if (registry.GetByUsername(req.Username) is not null)
            {
                return Results.Conflict("Username already taken.");
            }

            PlayerRecord player = registry.Register(req.Username);

            // Return the key ONCE — player must save it themselves
            return Results.Ok(new
            {
                player.Username,
                player.ApiKey,
            });
        });

        group.MapGet("/exists/{username}", (string username, PlayerRegistry registry) =>
    Results.Ok(new { exists = registry.GetByUsername(username) is not null }));

        return app;
    }

    public static IEndpointRouteBuilder MapPlayersEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/players");

        group.MapPost("/register", (RegisterRequest req, PlayerRegistry registry) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username))
            {
                return Results.BadRequest("Username is required.");
            }

            if (registry.GetByUsername(req.Username) is not null)
            {
                return Results.Conflict("Username already taken.");
            }

            PlayerRecord player = registry.Register(req.Username);

            // Return the key ONCE — player must save it themselves
            return Results.Ok(new
            {
                player.Username,
                player.ApiKey,
            });
        });

        group.MapGet("/exists/{username}", (string username, PlayerRegistry registry) =>
    Results.Ok(new { exists = registry.GetByUsername(username) is not null }));

        return app;
    }
}

public sealed class StartSessionRequest
{
    public Guid Id { get; set; }
    public string WorldName { get; set; }
    public string Port { get; set; }
}

public sealed record RegisterRequest(string Username);