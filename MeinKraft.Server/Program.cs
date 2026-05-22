using MeinKraft.Extensions;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
//builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

builder.Services.AddSharedServices();
builder.Services.AddServerServices(builder.Configuration);
builder.Services.AddServerMods();
builder.Services.AddSingleton<PlayerRegistry>();
builder.Services.AddServerWorkerInfrastructure();

builder.Logging.AddSerilog();

//builder.Host.UseSerilog();

WebApplication app = builder.Build();

app.MapGameSessionEndpoints();
app.MapPlayersEndpoints();
app.MapWorldEndpoints();

var publicPaths = new[] { "/", "/api/players/register", "/api/players/exists" };

app.Use(async (ctx, next) =>
{
    if (publicPaths.Any(p => ctx.Request.Path.StartsWithSegments(p)))
    {
        await next();
        return;
    }

    if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var key))
    {
        ctx.Response.StatusCode = 401;
        return;
    }

    var registry = ctx.RequestServices.GetRequiredService<PlayerRegistry>();
    var player = registry.Authenticate(key!);
    if (player is null)
    {
        ctx.Response.StatusCode = 401;
        return;
    }

    ctx.Items["player"] = player;
    await next();
});

app.MapGet("/", () => Results.Ok("MeinKraft server is running."));

app.Run();