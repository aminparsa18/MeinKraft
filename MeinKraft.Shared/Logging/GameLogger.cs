using Serilog;

/// <inheritdoc cref="IGameLogger"/>
public sealed class GameLogger : IGameLogger
{
    // Both loggers are thin ForContext wrappers around the shared root logger.
    // They enrich every event with the GameChannel property that LoggingSetup's
    // sub-logger filters use to route entries to the correct file.
    public ILogger Client { get; } =
        Log.Logger.ForContext(LoggingSetup.ChannelProperty, LoggingSetup.ClientChannel);

    public ILogger Server { get; } =
        Log.Logger.ForContext(LoggingSetup.ChannelProperty, LoggingSetup.ServerChannel);
}