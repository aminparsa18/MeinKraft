using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Filters;

/// <summary>
/// One-stop Serilog configuration and DI registration.
///
/// Call <see cref="AddGameLogging"/> from your host/service-collection setup.
/// Everything else in the codebase either injects <c>ILogger&lt;T&gt;</c>
/// (Microsoft.Extensions.Logging — routed through Serilog automatically) or
/// injects <see cref="IGameLogger"/> for explicit Client/Server channel control.
///
/// NuGet packages required:
///   Serilog
///   Serilog.Sinks.File
///   Serilog.Sinks.Console          (optional)
///   Serilog.Extensions.Logging     ← bridges MEL → Serilog
/// </summary>
public static class LoggingSetup
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>Property key written by <see cref="IGameLogger"/> to tag log entries.</summary>
    internal const string ChannelProperty = "GameChannel";

    internal const string ClientChannel = "Client";
    internal const string ServerChannel = "Server";

    private const string OutputTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

    // ── DI entry point ────────────────────────────────────────────────────────

    /// <summary>
    /// Configures Serilog and registers both the MEL bridge and
    /// <see cref="IGameLogger"/> with the service collection.
    ///
    /// <para>File layout under <paramref name="logDirectory"/>:</para>
    /// <code>
    ///   logs/
    ///     client-20260504T10.log    ← client-only, rolls every hour
    ///     server-20260504T10.log    ← server-only, rolls every hour
    ///     combined-20260504T10.log  ← everything, rolls every hour
    /// </code>
    /// </summary>
    /// <param name="services">The DI service collection to register into.</param>
    /// <param name="minimumLevel">Lowest level written to any sink.</param>
    /// <param name="enableConsole">Also write to stdout (useful in server / headless mode).</param>
    public static IServiceCollection AddGameLogging(
        this IServiceCollection services,
        LogEventLevel minimumLevel = LogEventLevel.Debug,
        bool enableConsole = false)
    {
        string dir = Path.Combine(GameStorePath.GetStorePath(), "logs");
        _ = Directory.CreateDirectory(dir);

        LoggerConfiguration cfg = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()

            // ── Client file — only entries tagged with Channel=Client ─────────
            .WriteTo.Logger(sub => sub
                .Filter.ByIncludingOnly(
                    Matching.WithProperty<string>(ChannelProperty, v => v == ClientChannel))
                .WriteTo.File(
                    path: Path.Combine(dir, "client-.log"),
                    rollingInterval: RollingInterval.Hour,
                    retainedFileCountLimit: 48,          // 48 h of hourly files
                    outputTemplate: OutputTemplate,
                    shared: true))

            // ── Server file — only entries tagged with Channel=Server ─────────
            .WriteTo.Logger(sub => sub
                .Filter.ByIncludingOnly(
                    Matching.WithProperty<string>(ChannelProperty, v => v == ServerChannel))
                .WriteTo.File(
                    path: Path.Combine(dir, "server-.log"),
                    rollingInterval: RollingInterval.Hour,
                    retainedFileCountLimit: 48,
                    outputTemplate: OutputTemplate,
                    shared: true));

        if (enableConsole)
        {
            cfg = cfg.WriteTo.Console(
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        // Assign to the global static so the pre-DI bootstrap logger
        // (used by CrashReporter.EnableGlobalExceptionHandling) can also
        // forward through the fully configured pipeline once it is ready.
        Log.Logger = cfg.CreateLogger();

        // Wire Serilog as the backend for Microsoft.Extensions.Logging.
        // Every ILogger<T> injected anywhere in the app now flows to Serilog.
        _ = services.AddLogging(builder =>
        {
            _ = builder.ClearProviders();
            _ = builder.AddSerilog(Log.Logger, dispose: true);
        });

        // Register the game-specific channel wrapper.
        _ = services.AddSingleton<IGameLogger, GameLogger>();

        return services;
    }
}