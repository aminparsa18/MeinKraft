using Microsoft.Extensions.DependencyInjection;

namespace MeinKraft.Extensions;

public static class BuilderSharedServicesExtensions
{
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        _ = services.AddSingleton<IVoxelMap, VoxelMap>();
        _ = services.AddSingleton<ILanguageService, LanguageService>();
        _ = services.AddSingleton<IBlockRegistry, BlockRegistry>();
        _ = services.AddSingleton<ICompression, CompressionGzip>();

        _ = services.AddGameLogging(
                   minimumLevel: Serilog.Events.LogEventLevel.Debug,
                   enableConsole: true);

        _ = services.AddSingleton<CrashReporter>();

        _ = services.AddMessagePipe();

        return services;
    }
}