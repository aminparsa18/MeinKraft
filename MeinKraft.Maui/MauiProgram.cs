using MeinKraft.Extensions;
using MeinKraft.Maui.Services;
using Plugin.Maui.Audio;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace MeinKraft.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .AddAudio()
            .ConfigureFonts(fonts => fonts.AddFont("PressStart2P-Regular.ttf", "PressStart2PRegular"));
        builder.Services.AddSharedServices();
        builder.Services.AddClientServices();

        builder.Services.AddClientMods();

        builder.Services.AddSingleton<IAssetManager, AssetManager>();
        builder.Services.AddSingleton<IAudioService, AudioService>();

        builder.Services.AddWorkerInfrastructure();

        string serverUrl = "http://192.168.0.101:8276";
        string? apiKey = Microsoft.Maui.Storage.Preferences.Get("api_key", null); // null until user registers

        builder.Services.AddApiServices(serverUrl, apiKey);

        builder.Services.AddSingleton<MauiGameWindowService>();
        builder.Services.AddSingleton<IGameWindowService>(sp =>
            sp.GetRequiredService<MauiGameWindowService>());

        return builder.Build();
    }
}
