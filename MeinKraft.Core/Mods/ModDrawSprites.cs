/// <summary>
/// Renders billboard sprites for entities (e.g. particles, projectile effects) in the 3D world.
/// </summary>
public class ModDrawSprites : ModBase
{
    private const float SpriteScale = 0.02f;
    private readonly IMeshDrawer meshDrawer;

    public ModDrawSprites(IMeshDrawer meshDrawer, IGame game) : base(game)
    {
        this.meshDrawer = meshDrawer;
    }

    public override void OnRender3d(float deltaTime)
    {
        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Entity entity = Game.Entities[i];
            if (entity?.Sprite == null)
            {
                continue;
            }

            Sprite b = entity.Sprite;
            int? frame = null;
            if (b.AnimationCount > 0)
            {
                float progress = 1f - (entity.Expires.TimeLeft / entity.Expires.TotalTime);
                frame = (int)(progress * ((b.AnimationCount * b.AnimationCount) - 1));
            }

            meshDrawer.GLMatrixModeModelView();
            meshDrawer.GLPushMatrix();
            meshDrawer.GLTranslate(b.PositionX, b.PositionY, b.PositionZ);
            VectorUtils.Billboard(meshDrawer);
            meshDrawer.GLScale(SpriteScale, SpriteScale, SpriteScale);
            meshDrawer.GLTranslate(-b.Size / 2, -b.Size / 2, 0);
            Game.Draw2dTexture(Game.GetTexture(b.Image), 0, 0, b.Size, b.Size, frame, b.AnimationCount, ColorUtils.ColorFromArgb(255, 255, 255, 255), true);
            meshDrawer.GLPopMatrix();
        }
    }
}