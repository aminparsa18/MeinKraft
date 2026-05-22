/// <summary>
/// Lists, creates, and deletes saved worlds for the authenticated player.
/// Worlds are stored under saves/{playerId}/{worldName}.mddbs on the server.
/// </summary>
public static class WorldEndpoints
{
    public static IEndpointRouteBuilder MapWorldEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/worlds");

        // GET /api/worlds — list all worlds for the current player
        group.MapGet("/", (HttpContext ctx) =>
        {
            var player = GetPlayer(ctx);
            string saveDir = PlayerSaveDir(player.Id);
            Directory.CreateDirectory(saveDir);

            var worlds = Directory
                .EnumerateFiles(saveDir, $"*{FileConstatns.DbFileExtension}")
                .Select(path => new WorldInfo(
                    Name: Path.GetFileNameWithoutExtension(path),
                    LastPlayed: File.GetLastWriteTimeUtc(path)))
                .OrderByDescending(w => w.LastPlayed)
                .ToList();

            return Results.Ok(worlds);
        });

        // POST /api/worlds — create a new world (sentinel file only)
        group.MapPost("/", (CreateWorldRequest req, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest("World name is required.");

            // Only reject actual invalid path characters, not spaces
            if (req.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return Results.BadRequest("World name contains invalid characters.");

            var player = GetPlayer(ctx);
            string saveDir = PlayerSaveDir(player.Id);
            Directory.CreateDirectory(saveDir);

            string path = WorldPath(player.Id, req.Name);
            if (File.Exists(path))
                return Results.Conflict($"A world named \"{req.Name}\" already exists.");

            // Sentinel file — actual data written on first save
            File.WriteAllText(path, string.Empty);

            return Results.Ok(new WorldInfo(req.Name, DateTime.UtcNow));
        });

        // DELETE /api/worlds/{worldName} — delete world and its data directory
        group.MapDelete("/{worldName}", (string worldName, HttpContext ctx) =>
        {
            var player = GetPlayer(ctx);
            string path = WorldPath(player.Id, worldName);

            if (!File.Exists(path))
                return Results.NotFound();

            File.Delete(path);

            string dataDir = Path.Combine(
                PlayerSaveDir(player.Id), worldName);

            if (Directory.Exists(dataDir))
                Directory.Delete(dataDir, recursive: true);

            return Results.NoContent();
        });

        return app;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PlayerRecord GetPlayer(HttpContext ctx)
        => (PlayerRecord)ctx.Items["player"]!;

    private static string PlayerSaveDir(string playerId)
        => Path.Combine(GameStorePath.gamepathsaves, playerId);

    private static string WorldPath(string playerId, string worldName)
        => GameStorePath.WorldSavePath(playerId, worldName);
}

public sealed record WorldInfo(string Name, DateTime LastPlayed);
public sealed record CreateWorldRequest(string Name);