namespace MeinKraft.Mods.War;

public class WaterSimple : IMod
{
    private int Water;
    private int Sponge;
    private readonly int spongerange = 2;
    private IServerModManager? m;

    public void PreStart(IServerModManager m) => m.RequireMod("CoreBlocks");

    public void Start(IServerModManager manager, IModEvents modEvents)
    {
        m = manager;
        modEvents.BlockBuild += BlockBuild;
        modEvents.BlockDelete += BlockDelete;
        Water = m.GetBlockId("Water");
        Sponge = m.GetBlockId("Sponge");
    }

    private void BlockBuild(BlockBuildArgs args) => BlockChange(args.Player, args.X, args.Y, args.Z);

    private void BlockDelete(BlockDeleteArgs args) => BlockChange(args.Player, args.X, args.Y, args.Z);

    private void Update()
    {
        object enablewater = m.GetGlobalDataNotSaved("enablewater");
        if (enablewater == null || (bool)enablewater == false)
        {
            return;
        }

        if ((DateTime.UtcNow - lastflood).TotalSeconds > 1)
        {
            lastflood = DateTime.UtcNow;
            List<Vector3i> curtoflood = new(toflood.Keys);
            foreach (Vector3i v in curtoflood)
            {
                Flood(v);
                toflood.Remove(v);
            }
        }
    }

    private bool IsSpongeNear(int x, int y, int z)
    {
        for (int xx = x - spongerange; xx <= x + spongerange; xx++)
        {
            for (int yy = y - spongerange; yy <= y + spongerange; yy++)
            {
                for (int zz = z - spongerange; zz <= z + spongerange; zz++)
                {
                    if (m.IsValidPos(xx, yy, zz) && m.GetBlock(xx, yy, zz) == Sponge)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public void BlockChange(int player, int x, int y, int z)
    {
        object enablewater = m.GetGlobalDataNotSaved("enablewater");
        if (enablewater == null || (bool)enablewater == false)
        {
            return;
        }

        flooded = new Dictionary<Vector3i, Vector3i>();
        //sponge just built.
        if (m.IsValidPos(x, y, z) && m.GetBlock(x, y, z) == Sponge)
        {
            for (int xx = x - spongerange; xx <= x + spongerange; xx++)
            {
                for (int yy = y - spongerange; yy <= y + spongerange; yy++)
                {
                    for (int zz = z - spongerange; zz <= z + spongerange; zz++)
                    {
                        if (m.IsValidPos(xx, yy, zz) && IsWater(m.GetBlock(xx, yy, zz)))
                        {
                            //tosetempty.Add(new Vector3i(xx, yy, zz));
                            m.SetBlock(xx, yy, zz, 0);
                        }
                    }
                }
            }
        }
        //maybe sponge destroyed. todo faster test.
        for (int xx = x - spongerange; xx <= x + spongerange; xx++)
        {
            for (int yy = y - spongerange; yy <= y + spongerange; yy++)
            {
                for (int zz = z - spongerange; zz <= z + spongerange; zz++)
                {
                    if (m.IsValidPos(xx, yy, zz) && m.GetBlock(xx, yy, zz) == 0)
                    {
                        BlockChangeFlood(xx, yy, zz);
                    }
                }
            }
        }

        BlockChangeFlood(x, y, z);
        //var v = new Vector3i(x, y, z);
        //tosetwater.Sort((a, b) => Distance(v, a).CompareTo(Distance(v, b)));
    }

    private static float Distance(Vector3i a, Vector3i b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private bool IsWater(int block) => block == Water;

    private void BlockChangeFlood(int x, int y, int z)
    {
        //water here
        if (m.IsValidPos(x, y, z)
            && IsWater(m.GetBlock(x, y, z)))
        {
            Flood(new Vector3i(x, y, z));
            return;
        }
        //water around
        foreach (Vector3i vv in BlocksAround(new Vector3i(x, y, z)))
        {
            if (m.IsValidPos(vv.X, vv.Y, vv.Z) &&
                IsWater(m.GetBlock(vv.X, vv.Y, vv.Z)))
            {
                Flood(vv);
                return;
            }
        }
    }

    private Dictionary<Vector3i, Vector3i> flooded = new();
    //public List<Vector3i> tosetwater = new List<Vector3i>();
    //public List<Vector3i> tosetempty = new List<Vector3i>();
    private readonly Dictionary<Vector3i, Vector3i> toflood = new();
    private DateTime lastflood;

    private void Flood(Vector3i v)
    {
        if (!m.IsValidPos(v.X, v.Y, v.Z))
        {
            return;
        }

        if (flooded.ContainsKey(v))
        {
            return;
        }

        flooded.Add(v, v);
        foreach (Vector3i vv in BlocksAround(v))
        {
            if (!m.IsValidPos(vv.X, vv.Y, vv.Z))
            {
                continue;
            }

            var type = m.GetBlock(vv.X, vv.Y, vv.Z);
            if (type == 0 && (!IsSpongeNear(vv.X, vv.Y, vv.Z)))
            {
                //tosetwater.Add(vv);
                m.SetBlock(vv.X, vv.Y, vv.Z, Water);
                toflood[vv] = vv;
            }
        }
    }

    private static IEnumerable<Vector3i> BlocksAround(Vector3i pos)
    {
        yield return new Vector3i(pos.X - 1, pos.Y, pos.Z);
        yield return new Vector3i(pos.X + 1, pos.Y, pos.Z);
        yield return new Vector3i(pos.X, pos.Y - 1, pos.Z);
        yield return new Vector3i(pos.X, pos.Y + 1, pos.Z);
        yield return new Vector3i(pos.X, pos.Y, pos.Z - 1);
        //yield return new Vector3i(pos.X, pos.Y, pos.Z + 1); //water does not flow up.
    }

    public struct Vector3i
    {
        public Vector3i(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        public int X;
        public int Y;
        public int Z;
    }
}
