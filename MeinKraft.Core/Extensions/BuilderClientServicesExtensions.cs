using MessagePipe;
using Microsoft.Extensions.DependencyInjection;

namespace MeinKraft.Extensions;

public static class BuilderClientServicesExtensions
{
    public static IServiceCollection AddClientServices(this IServiceCollection services)
    {
       // services.AddSingleton<GameWindowNative>();

        services.AddSingleton<IGameExitService, GameExitService>();
     //   services.AddSingleton<IGameWindowService, GameWindowService>();
        services.AddSingleton<ICameraService, CameraService>();
        services.AddSingleton<IPreferences, Preferences>();
        services.AddSingleton<IOpenGlService, OpenGlService>();
        services.AddSingleton<IFrustumCulling, FrustumCulling>();
        services.AddSingleton<IMeshBatcher, MeshBatcher>();
        services.AddSingleton<IMeshDrawer, MeshDrawer>();
        services.AddSingleton<ITerrainChunkTesselator, TerrainChunkTesselator>();
        services.AddSingleton<IBlockChangeNotifier, BlockChangeNotifier>();
        services.AddSingleton<IDisplayService, DisplayService>();

        services.AddSingleton<ILightManager>(sp =>  
            new LightManager(sp.GetRequiredService<IVoxelMap>(), sp.GetRequiredService<IBlockRegistry>(),
            new Lazy<ILightingWorkQueue>(() => sp.GetRequiredService<ILightingWorkQueue>()),
            sp.GetRequiredService<ISubscriber<BlockChangedEvent>>()));

        services.AddSingleton<IGame, Game>();

        return services;
    }
}