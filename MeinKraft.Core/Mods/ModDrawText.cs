using System.Numerics;

/// <summary>
/// Renders floating 3D text labels attached to entities, facing the player when nearby.
/// </summary>
public class ModDrawText : ModBase
{
    private const float TextScale = 0.005f;
    private const float TextDrawDistance = 20f;

    private readonly IMeshDrawer meshDrawer;

    public ModDrawText(IMeshDrawer meshDrawer, IGame game) : base(game)
    {
        this.meshDrawer = meshDrawer;
    }

    public override void OnRender3d(float deltaTime)
    {
        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Entity e = Game.Entities[i];
            if (e?.DrawText == null)
            {
                continue;
            }

            if (e.NetworkPosition != null && !e.NetworkPosition.PositionLoaded)
            {
                continue;
            }

            EntityDrawText p = e.DrawText;
            float posX = (-MathF.Sin(e.Position.RotY) * p.X) + e.Position.X;
            float posY = p.Y + e.Position.Y;
            float posZ = (MathF.Cos(e.Position.RotY) * p.Z) + e.Position.Z;

            bool nearEnough = Vector3.Distance(new Vector3(Game.Player.Position.X, Game.Player.Position.Y, Game.Player.Position.Z), new Vector3(posX, posY, posZ)) < TextDrawDistance;
            bool altHeld = Game.KeyboardState[KeyConstants.KeyAltLeft] || Game.KeyboardState[KeyConstants.KeyAltRight];
            if (!nearEnough && !altHeld)
            {
                continue;
            }

            DrawText(e, p, posX, posY, posZ);
        }
    }

    private void DrawText(Entity e, EntityDrawText p, float posX, float posY, float posZ)
    {
        meshDrawer.GLPushMatrix();
        meshDrawer.GLTranslate(posX, posY, posZ);
        meshDrawer.GLRotate(180, 1, 0, 0);
        meshDrawer.GLRotate(float.RadiansToDegrees(e.Position.RotY), 0, 1, 0);
        meshDrawer.GLScale(TextScale, TextScale, TextScale);
        Game.Draw2dText(p.Text, GameFonts.Default, -Game.TextSizeWidth(p.Text, 14) / 2, 0, ColorUtils.ColorFromArgb(255, 255, 255, 255), true);
        meshDrawer.GLPopMatrix();
    }
}