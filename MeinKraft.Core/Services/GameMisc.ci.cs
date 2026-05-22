using MeinKraft;

public class Entity
{
    public Entity()
    {
        Scripts = [];
    }

    public Expiry Expires { get; set; }
    public Sprite Sprite { get; set; }
    public Grenade Grenade { get; set; }
    public Bullet Bullet { get; set; }
    public Minecart Minecart { get; set; }
    public PlayerDrawInfo PlayerDrawInfo { get; set; }

    public IList<IEntityScript> Scripts { get; set; }

    // network
    public EntityPosition NetworkPosition { get; set; } 
    public EntityPosition Position { get; set; }
    public DrawName DrawName { get; set; }
    public EntityDrawModel DrawModel { get; set; }
    public EntityDrawText DrawText { get; set; }
    public Packet_ServerExplosion Push {  get; set; }
    public bool IsUsable { get; set; }
    public Packet_ServerPlayerStats PlayerStats { get; set; }
    public EntityDrawArea DrawArea { get; set; }

    /// <summary>Creates a bullet entity travelling from <paramref name="fromX/Y/Z"/> to <paramref name="toX/Y/Z"/>.</summary>
    public static Entity CreateBullet(
        float fromX, float fromY, float fromZ,
        float toX, float toY, float toZ,
        float speed)
    {
        return new Entity
        {
            Bullet = new Bullet
            {
                FromX = fromX,
                FromY = fromY,
                FromZ = fromZ,
                ToX = toX,
                ToY = toY,
                ToZ = toZ,
                Speed = speed,
            },
            Sprite = new Sprite
            {
                Image = "Sponge.png",
                Size = 4,
                AnimationCount = 0,
            },
        };
    }
}

public class EntityDrawArea
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int SizeX { get; set; }
    public int SizeY { get; set; }
    public int SizeZ { get; set; }
    public bool Visible { get; set; }
}

public class EntityPosition
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float RotX  { get; set; }
    public float RotY { get; set; }
    public float RotZ { get; set; }
    public bool PositionLoaded { get; set; }
    public int LastUpdateMilliseconds { get; set; }
}

public class EntityDrawModel
{
    public float EyeHeight { get; set; }
    public string Model { get; set; }
    public float ModelHeight { get; set; }
    public string Texture_ { get; set; }
    public bool DownloadSkin { get; set; }
    public int CurrentTexture { get; set; } = -1;
    public HttpResponse SkinDownloadResponse { get; set; }
    public AnimatedModelRenderer Renderer { get; set; }
}

public class EntityDrawText
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float RotX { get; set; }
    public float RotY { get; set; }
    public float RotZ { get; set; }
    public string Text { get; set; }
}

public class VisibleDialog
{
    public string Key { get; set; }
    public Dialog Value { get; set; }
    public ModScreen Screen { get; set; }
}

public class MenuState
{
    internal int selected;
}

public class OnCrashHandlerLeave : OnCrashHandler
{
    public static OnCrashHandlerLeave Create(IGame game)
    {
        OnCrashHandlerLeave oncrash = new()
        {
            g = game
        };
        return oncrash;
    }
    private IGame g;
    public override void OnCrash() => g.SendLeave(PacketLeaveReason.Crash);
}