using Microsoft.Extensions.Logging;
using Serilog;
using System.Text;

/// <summary>
/// Crash reporter — handles fatal exceptions, notifies the user, and exits.
///
/// The class now has two distinct modes of operation:
///
/// 1. <b>DI mode (normal path)</b> — inject <see cref="CrashReporter"/> and
///    <see cref="OnCrash"/> is available for clean-up hooks. The logger is
///    the fully configured Serilog pipeline wired through MEL.
///
/// 2. <b>Pre-DI bootstrap (static path)</b> — call
///    <see cref="EnableGlobalExceptionHandling"/> from <c>Main()</c> before the
///    host is built. Any unhandled exception before DI is ready is written to
///    a dedicated crash file and then re-reported through the DI instance once
///    the host starts (if it ever does).
///
/// Logging configuration (sinks, rolling, file paths) lives entirely in
/// <see cref="LoggingSetup.AddGameLogging"/>; this class no longer owns a
/// <c>LoggerConfiguration</c>.
/// </summary>
public sealed class CrashReporter
{
    // ── Configuration ────────────────────────────────────────────────────────

    /// <summary>
    /// Maximum time (ms) to wait for the <see cref="OnCrash"/> delegate
    /// before aborting. Prevents a hung cleanup callback from blocking shutdown.
    /// </summary>
    public static int OnCrashTimeoutMs { get; set; } = 5_000;

    private const string CrashFileName = "crash.log";
    private const string OutputTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

    // ── State ────────────────────────────────────────────────────────────────

    private static bool s_isConsole;
    private static ILogger<CrashReporter>? s_bootstrapLogger;
    private readonly ILogger<CrashReporter> _logger;
    private readonly string _crashFilePath;

    /// <summary>Optional cleanup hook — called (with timeout) before shutdown.</summary>
    public Action? OnCrash { get; set; }
    public Action<bool>? SetCursorVisible { get; set; }
    public Action<string, string>? ShowErrorDialog { get; set; }

    // ── Constructor (DI) ──────────────────────────────────────────────────────

    /// <param name="logger">Injected by the DI container.</param>
    public CrashReporter(ILogger<CrashReporter> logger)
    {
        _logger = logger;
        _crashFilePath = Path.Combine(GameStorePath.GetStorePath(), "logs", CrashFileName);
        s_bootstrapLogger = logger;
    }

    // ── Global exception handling (pre-DI bootstrap) ──────────────────────────

    /// <summary>
    /// Registers a global unhandled-exception handler.
    ///
    /// Call once from <c>Main()</c>, before <c>Host.Build()</c>, so that
    /// exceptions thrown during host construction are still captured.
    ///
    /// After <c>Host.Start()</c> the DI-constructed <see cref="CrashReporter"/>
    /// automatically takes over (see constructor above).
    /// </summary>
    /// <param name="isConsole">
    ///   <c>true</c>  — errors written to stderr.<br/>
    ///   <c>false</c> — errors shown in a <c>MessageBox</c>.
    /// </param>
    public void EnableGlobalExceptionHandling(bool isConsole)
    {
        s_isConsole = isConsole;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (s_isConsole)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("Unhandled exception — application is terminating.");
            Console.ResetColor();
        }

        Exception? ex = e.ExceptionObject as Exception;

        if (s_bootstrapLogger is not null)
        {
            s_bootstrapLogger.LogCritical(ex, "Unhandled exception");
            new CrashReporter(s_bootstrapLogger).Crash(ex);
        }
        else
        {
            // Host not yet built — write directly via Serilog's global logger
            // and a standalone crash file.
            Log.Fatal(ex, "Unhandled exception (pre-DI)");
            string crashPath = Path.Combine(
                GameStorePath.GetStorePath(), "logs", CrashFileName);
            WriteCrashFile(ex, crashPath);
            DisplayToUser(BuildSummary(ex, crashPath));
            Environment.Exit(1);
        }
    }

    // ── Core crash logic ──────────────────────────────────────────────────────

    /// <summary>
    /// Logs <paramref name="exCrash"/>, runs the optional cleanup callback,
    /// displays an error to the user, then exits.
    /// </summary>
    public void Crash(Exception? exCrash)
    {
        // 1. Log via MEL → Serilog → combined log (and client/server if enriched)
        _logger.LogCritical(exCrash, "Critical error — application is terminating");

        for (Exception? inner = exCrash?.InnerException; inner is not null; inner = inner.InnerException)
        {
            _logger.LogCritical(inner, "Inner exception");
        }

        // 2. Write a dedicated crash.log — overwrites each run, no rolling noise,
        //    always contains exactly the most recent failure.
        WriteCrashFile(exCrash, _crashFilePath);

        // 3. Run optional cleanup with a hard timeout
        RunOnCrashCallback();

        // 4. Flush Serilog — critical for async sinks before Environment.Exit
        Serilog.Log.CloseAndFlush();

        // 5. Tell the user
        DisplayToUser(BuildSummary(exCrash, _crashFilePath));

        if (s_isConsole)
        {
            Console.WriteLine("Press any key to shut down...");
            _ = Console.ReadLine();
        }

        Environment.Exit(1);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RunOnCrashCallback()
    {
        if (OnCrash is null)
        {
            return;
        }

        Task task = Task.Run(() => OnCrash());

        if (!task.Wait(OnCrashTimeoutMs))
        {
            _logger.LogWarning("OnCrash() did not complete within {Timeout} ms — skipped", OnCrashTimeoutMs);
        }

        if (task.IsFaulted)
        {
            _logger.LogError(task.Exception, "OnCrash() threw an exception");
        }
    }

    /// <summary>
    /// Writes a standalone <c>crash.log</c> that overwrites on every crash.
    /// No rolling, no noise from previous sessions — always the last failure only.
    /// </summary>
    private static void WriteCrashFile(Exception? ex, string path)
    {
        try
        {
            using Serilog.Core.Logger crashLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    path: path,
                    rollingInterval: RollingInterval.Infinite,  // never roll — overwrite intent
                    retainedFileCountLimit: 1,                   // keep only the latest
                    outputTemplate: OutputTemplate,
                    shared: false)
                .CreateLogger();

            crashLogger.Fatal(ex, "Critical error — application is terminating");

            for (Exception? inner = ex?.InnerException; inner is not null; inner = inner.InnerException)
            {
                crashLogger.Fatal(inner, "Inner exception");
            }
        }
        catch
        {
            // Writing the crash file must never itself crash the crash reporter.
        }
    }

    private static string BuildSummary(Exception? ex, string? crashFilePath)
    {
        StringBuilder sb = new();
        _ = sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  Critical Error");

        if (ex is not null)
        {
            _ = sb.AppendLine(ex.Message);
        }

        if (crashFilePath is not null)
        {
            _ = sb.AppendLine();
            _ = sb.AppendLine($"Crash report written to:\n  {crashFilePath}");
        }

        _ = sb.AppendLine();
        _ = sb.AppendLine("Check the logs/ folder for the full crash report.");
        return sb.ToString();
    }

    private void DisplayToUser(string message)
    {
        if (s_isConsole)
        {
            Console.Error.WriteLine(message);
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                SetCursorVisible?.Invoke(true);
                Thread.Sleep(50);
            }

            ShowErrorDialog?.Invoke(message, "Critical Error");
        }
    }
}