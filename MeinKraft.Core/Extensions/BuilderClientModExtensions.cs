using MeinKraft.Mods;
using Microsoft.Extensions.DependencyInjection;

namespace MeinKraft.Extensions;

public static class BuilderClientModExtensions
{
    public static IServiceCollection AddClientMods(this IServiceCollection services)
    {
        // ── Player logic ──────────────────────────────────────────────────────
        services.AddScoped<IModBase, ModNetworkProcess>();
        services.AddScoped<IModBase, ModNetworkEntity>();
        services.AddScoped<IModBase, ModFallDamageToPlayer>();
        services.AddScoped<IModBase, ModBlockDamageToPlayer>();
        services.AddScoped<IModBase, ModLoadPlayerTextures>();
        services.AddScoped<IModBase, ModSendPosition>();
        services.AddScoped<IModBase, ModInterpolatePositions>();
        services.AddScoped<IModBase, ModPush>();
        services.AddScoped<IModBase, ModFly>();

        // ── Camera ────────────────────────────────────────────────────────────
        services.AddScoped<IModBase, ModAutoCamera>();
        services.AddScoped<IModBase, ModCameraKeys>();
        services.AddScoped<IModBase, ModCamera>();

        // ── Gameplay mechanics ────────────────────────────────────────────────
        services.AddScoped<IModBase, ModRail>();
        services.AddScoped<IModBase, ModCompass>();
        services.AddScoped<IModBase, ModGrenade>();
        services.AddScoped<IModBase, ModBullet>();
        services.AddScoped<IModBase, ModExpire>();
        services.AddScoped<IModBase, ModPicking>();

        // ── Inventory / ammo ──────────────────────────────────────────────────
        services.AddScoped<IModBase, ModReloadAmmo>();
        services.AddScoped<IModBase, ModSendActiveMaterial>();
        services.AddScoped<IModBase, ModGuiCrafting>();
        services.AddScoped<IModBase, ModGuiInventory>();

        // ── Audio ─────────────────────────────────────────────────────────────
        services.AddScoped<IModBase, ModWalkSound>();
        services.AddScoped<IModBase, ModAudio>();

        // ── World rendering ───────────────────────────────────────────────────
        services.AddScoped<IModBase, ModSkySphereAnimated>();
        services.AddScoped<IModBase, ModSunMoon>();
        services.AddScoped<IModBase, ModDrawTerrain>();
        services.AddScoped<IModBase, ModDrawSprites>();
        services.AddScoped<IModBase, ModDrawMinecarts>();
        services.AddScoped<IModBase, ModDrawLinesAroundSelectedBlock>();

        // ── Entity / player rendering ─────────────────────────────────────────
        services.AddScoped<IModBase, ModDrawPlayers>();
        services.AddScoped<IModBase, ModDrawPlayerNames>();
        services.AddScoped<IModBase, ModDrawTestModel>();
        services.AddScoped<IModBase, ModClearInactivePlayersDrawInfo>();

        // ── HUD / 2D overlay ──────────────────────────────────────────────────
        services.AddScoped<IModBase, ModDrawHand3d>();
        services.AddScoped<IModBase, ModDrawText>();
        services.AddScoped<IModBase, ModDraw2dMisc>();
        services.AddScoped<IModBase, ModFpsHistoryGraph>();

        // ── GUI (topmost — rendered last) ─────────────────────────────────────
        services.AddScoped<IModBase, ModDialog>();
        services.AddScoped<IModBase, ModGuiTouchButtons>();
        services.AddScoped<IModBase, ModGuiPlayerStats>();
        services.AddScoped<IModBase, ModGuiChat>();
        services.AddScoped<IModBase, ModScreenshot>();

        services.AddSingleton<IModRegistry, ModRegistry>();

        return services;
    }
}