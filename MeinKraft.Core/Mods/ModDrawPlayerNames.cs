using OpenTK.Mathematics;

/// <summary>
/// Renders player name tags and health bars above entity models in the 3D world.
/// </summary>
public class ModDrawPlayerNames : ModBase
{
    private const float NameTagHeightOffset = 0.7f;
    private const float NameTagScale = 0.02f;
    private const float NameTagDrawDistance = 20f;

    private readonly IMeshDrawer meshDrawer;

    public ModDrawPlayerNames(IMeshDrawer meshDrawer, IGame game) : base(game)
    {
        this.meshDrawer = meshDrawer;
    }

    public override void OnRender3d(float deltaTime)
    {
        if (true) // TODO: fix after multiplayer runs
            return; 
        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Entity e = Game.Entities[i];
            if (e?.DrawName == null)
            {
                continue;
            }

            if (i == Game.LocalPlayerId)
            {
                continue;
            }

            if (e.NetworkPosition != null && !e.NetworkPosition.PositionLoaded)
            {
                continue;
            }

            DrawName p = e.DrawName;
            if (p.OnlyWhenSelected)
            {
                continue;
            }

            float posX = p.TextX + e.Position.X;
            float posY = p.TextY + e.Position.Y + e.DrawModel.ModelHeight + NameTagHeightOffset;
            float posZ = p.TextZ + e.Position.Z;
            bool nearEnough = Vector3.Distance(new(Game.Player.Position.X, Game.Player.Position.Y, Game.Player.Position.Z), new(posX, posY, posZ)) < NameTagDrawDistance;
            bool altHeld = Game.KeyboardState[KeyConstants.KeyAltLeft] || Game.KeyboardState[KeyConstants.KeyAltRight];
            if (!nearEnough && !altHeld)
            {
                continue;
            }

            DrawNameTag(p, posX, posY, posZ);
        }
    }

    private void DrawNameTag(DrawName p, float posX, float posY, float posZ)
    {
        meshDrawer.GLPushMatrix();
        meshDrawer.GLTranslate(posX, posY, posZ);
        VectorUtils.Billboard(meshDrawer);
        meshDrawer.GLScale(NameTagScale, NameTagScale, NameTagScale);

        if (p.DrawHealth)
        {
            Game.Draw2dTexture(Game.GetOrCreateWhiteTexture(), -26, -11, 52, 12, null, 0, ColorUtils.ColorFromArgb(255, 0, 0, 0), false);
            Game.Draw2dTexture(Game.GetOrCreateWhiteTexture(), -25, -10, 50 * p.Health, 10, null, 0, ColorUtils.ColorFromArgb(255, 255, 0, 0), false);
        }

        Game.Draw2dText(p.Name, GameFonts.Default, -Game.TextSizeWidth(p.Name, 14) / 2, 0, ColorUtils.ColorFromArgb(255, 255, 255, 255), true);

        meshDrawer.GLPopMatrix();
    }
}