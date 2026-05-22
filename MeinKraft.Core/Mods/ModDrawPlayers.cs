using MeinKraft;
using OpenTK.Mathematics;
using System.Text;

/// <summary>
/// Renders animated player/entity models in the 3D world each frame.
/// </summary>
public class ModDrawPlayers : ModBase
{
    private readonly IGameWindowService _gameService;
    private readonly IVoxelMap _voxelMap;
    private readonly IFrustumCulling _frustumCulling;
    private readonly IMeshDrawer _meshDrawer;
    private readonly IOpenGlService _openGlService;
    private readonly ILightManager _lightManager;

    public ModDrawPlayers(IGameWindowService platform, IVoxelMap voxelMap, IFrustumCulling frustumCulling,
        IMeshDrawer meshDrawer, IOpenGlService openGlService, ILightManager lightManager, IGame game) : base(game)
    {
        this._gameService = platform;
        this._voxelMap = voxelMap;
        this._frustumCulling = frustumCulling;
        this._meshDrawer = meshDrawer;
        this._lightManager = lightManager;
        this._openGlService = openGlService;
    }

    public override void OnRender3d(float deltaTime)
    {
        Game.TotalTimeMilliseconds = _gameService.TimeMillisecondsFromStart;

        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Entity p = Game.Entities[i];
            if (p?.DrawModel == null)
            {
                continue;
            }

            if (i == Game.LocalPlayerId && !Game.EnableTppView)
            {
                continue;
            }

            if (p.NetworkPosition != null && !p.NetworkPosition.PositionLoaded)
            {
                continue;
            }

            if (!_frustumCulling.SphereInFrustum(p.Position.X, p.Position.Y, p.Position.Z, 3))
            {
                continue;
            }

            if (p.DrawModel.CurrentTexture == -1)
            {
                continue;
            }

            int cx = (int)p.Position.X / GameConstants.CHUNK_SIZE;
            int cy = (int)p.Position.Z / GameConstants.CHUNK_SIZE;
            int cz = (int)p.Position.Y / GameConstants.CHUNK_SIZE;
            if (_voxelMap.IsValidChunkPos(cx, cy, cz) && !_voxelMap.IsChunkRendered(cx, cy, cz))
            {
                continue;
            }

            p.PlayerDrawInfo ??= new PlayerDrawInfo();

            float shadow = (float)_lightManager.GetLight((int)p.Position.X, (int)p.Position.Z, (int)p.Position.Y) / GameConstants.maxlight;
            float speed = i == Game.LocalPlayerId ? GetLocalPlayerSpeed(Game) : GetNetworkPlayerSpeed(p, deltaTime);

            EnsureRenderer(p);
            DrawEntity(p, deltaTime, shadow, speed);
        }
    }

    /// <summary>Calculates movement speed for the local player based on physics velocity.</summary>
    private float GetLocalPlayerSpeed(IGame game)
    {
        game.Player.PlayerDrawInfo ??= new PlayerDrawInfo();

        float speed = new Vector3(
            game.playervelocity.X / 60f,
            game.playervelocity.Y / 60f,
            game.playervelocity.Z / 60f).Length * 1.5f;

        game.Player.PlayerDrawInfo.Moves = speed != 0;
        return speed;
    }

    /// <summary>Calculates movement speed for a network entity based on interpolated velocity.</summary>
    private static float GetNetworkPlayerSpeed(Entity p, float dt) => p.PlayerDrawInfo.Velocity.Length / dt * 0.04f;

    /// <summary>Loads and initializes the animated model renderer for an entity if not already done.</summary>
    private void EnsureRenderer(Entity p)
    {
        if (p.DrawModel.Renderer != null)
        {
            return;
        }

        p.DrawModel.Renderer = new AnimatedModelRenderer(_meshDrawer, _openGlService);
        byte[] data = Game.GetAssetFile(p.DrawModel.Model);
        int dataLength = Game.GetAssetFileLength(p.DrawModel.Model);
        if (data == null)
        {
            return;
        }

        string dataString = Encoding.UTF8.GetString(data, 0, dataLength);
        AnimatedModel model = AnimatedModelSerializer.Deserialize(dataString);
        p.DrawModel.Renderer.Start(Game, model);
    }

    /// <summary>Renders the entity's animated model at its current world position and orientation.</summary>
    private void DrawEntity(Entity p, float dt, float shadow, float speed)
    {
        _meshDrawer.GLPushMatrix();
        _meshDrawer.GLTranslate(p.Position.X, p.Position.Y, p.Position.Z);
        _meshDrawer.GLRotate(float.RadiansToDegrees(-p.Position.RotY + MathF.PI), 0, 1, 0);
        _openGlService.BindTexture2d(p.DrawModel.CurrentTexture);
        p.DrawModel.Renderer.Render(dt, float.RadiansToDegrees(p.Position.RotX + MathF.PI), true, p.PlayerDrawInfo.Moves, shadow);
        _meshDrawer.GLPopMatrix();
    }
}