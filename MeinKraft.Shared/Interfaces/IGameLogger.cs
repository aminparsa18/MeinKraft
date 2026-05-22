using Serilog;

/// <summary>
/// Provides two pre-configured Serilog loggers — one for the client process
/// and one for the server process — that route to their respective hourly
/// rolling log files while still flowing through the shared Serilog pipeline.
///
/// Inject this where you need explicit channel control. For everything else,
/// inject the standard <c>Microsoft.Extensions.Logging.ILogger&lt;T&gt;</c>
/// which also flows through Serilog automatically.
///
/// Usage:
/// <code>
///   public class ModDrawTerrain(IGameLogger log)
///   {
///       void SomeMethod() => log.Client.Information("Chunk rebuilt at {X} {Y} {Z}", x, y, z);
///   }
/// </code>
/// </summary>
public interface IGameLogger
{
    ILogger Client { get; }
    ILogger Server { get; }
}