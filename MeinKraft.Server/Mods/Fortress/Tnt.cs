namespace MeinKraft.Mods;

public class Tnt : IMod
{
    private IServerModManager m;
    private int tnt;
    private int adminium;
    private readonly Random rnd = new();
    private readonly Stack<Vector3i> tntStack = new();

    public int tntRange = 10; // sphere diameter
    public int tntMax = 10;

    public void PreStart(IServerModManager m) => m.RequireMod("CoreBlocks");
    public void Start(IServerModManager manager, IModEvents modEvents)
    {
        m = manager;
        SoundSet solidSounds = new()
        {
            Walk = ["walk1", "walk2", "walk3", "walk4"],
            Break = ["destruct"],
            Build = ["build"],
            Clone = ["clone"],
        };
        m.SetBlockType(46, "TNT", new BlockType()
        {
            TextureIdTop = "TNTTop",
            TextureIdBottom = "TNTBottom",
            TextureIdBack = "TNT",
            TextureIdFront = "TNT",
            TextureIdLeft = "TNT",
            TextureIdRight = "TNT",
            TextureIdForInventory = "TNT",
            DrawType = DrawType.Solid,
            WalkableType = WalkableType.Solid,
            Sounds = solidSounds,
            IsUsable = true,
        });
        tnt = m.GetBlockId("TNT");
        adminium = m.GetBlockId("Adminium");
        m.AddToCreativeInventory("TNT");
        m.AddCraftingRecipe("TNT", 1, "GoldBlock", 1);
        modEvents.BlockUse += UseTnt;
        m.RegisterPrivilege("use_tnt");
        m.RegisterTimer(UpdateTnt, 5);
    }

    private struct Vector3i(int x, int y, int z)
    {
        public int x = x;
        public int y = y;
        public int z = z;
    }

    private void UseTnt(BlockUseArgs args)
    {
        if (m.GetBlock(args.X, args.Y, args.Z) == tnt)
        {
            if (!m.PlayerHasPrivilege(args.Player, "use_tnt"))
            {
                m.SendMessage(args.Player, GameConstants.colorError + "Insufficient privileges to use TNT.");
                return;
            }

            if (tntStack.Count < tntMax)
                tntStack.Push(new Vector3i(args.X, args.Y, args.Z));
        }
    }

    private void UpdateTnt()
    {
        int now = 0;
        while (now++ < 3)
        {
            if (tntStack.Count == 0)
            {
                return;
            }

            Vector3i pos = tntStack.Pop();
            int nearestplayer = m.NearestPlayer(pos.x, pos.y, pos.z);
            m.PlaySoundAt(pos.x, pos.y, pos.z, "tnt.wav");

            for (int xx = 0; xx < tntRange; xx++)
            {
                for (int yy = 0; yy < tntRange; yy++)
                {
                    for (int zz = 0; zz < tntRange; zz++)
                    {
                        if (SphereEq(xx - ((tntRange - 1) / 2), yy - ((tntRange - 1) / 2), zz - ((tntRange - 1) / 2), tntRange / 2) <= 0)
                        {
                            Vector3i pos2 = new(pos.x + xx - (tntRange / 2),
                                                         pos.y + yy - (tntRange / 2),
                                                         pos.z + zz - (tntRange / 2));
                            if (!m.IsValidPos(pos2.x, pos2.y, pos2.z))
                            {
                                continue;
                            }

                            int block = m.GetBlock(pos2.x, pos2.y, pos2.z);
                            if (tntStack.Count < tntMax
                                && (pos2.x != pos.x || pos2.y != pos.y || pos2.z != pos.z)
                                && block == tnt)
                            {
                                tntStack.Push(pos2);
                            }
                            else
                            {
                                if ((block != 0)
                                    && (block != adminium)
                                    && !m.IsBlockFluid(block))
                                {
                                    m.SetBlock(pos2.x, pos2.y, pos2.z, 0);
                                    if (!m.IsCreative)
                                    {
                                        // chance to get some of destruced blocks
                                        if (rnd.NextDouble() < .20f)
                                        {
                                            if (nearestplayer != -1)
                                            {
                                                m.GrabBlock(nearestplayer, block);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            m.NotifyInventory(nearestplayer);
        }
    }

    private static int SphereEq(int x, int y, int z, int r) => (x * x) + (y * y) + (z * z) - (r * r);
}
