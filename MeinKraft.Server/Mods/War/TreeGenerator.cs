using LibNoise;

namespace MeinKraft.Mods.War;

public class TreeGenerator : IMod
{
    private IServerModManager? m;
    private readonly int treeCount = 20;
    private readonly Billow treenoise = new();
    private readonly Random _rnd = new();

    private int BLOCK_GRASS;
    private int BLOCK_OAKTRUNK;
    private int BLOCK_OAKLEAVES;
    private int BLOCK_APPLES;
    private int BLOCK_SPRUCETRUNK;
    private int BLOCK_SPRUCELEAVES;
    private int BLOCK_BIRCHTRUNK;
    private int BLOCK_BIRCHLEAVES;

    public void PreStart(IServerModManager m) => m.RequireMod("CoreBlocks");

    public void Start(IServerModManager manager, IModEvents modEvents)
    {
        m = manager;
        int Seed = m.Seed;
        treenoise.Seed = Seed + 2;
        treenoise.OctaveCount = 6;
        treenoise.Frequency = 1f / 180f;
        treenoise.Lacunarity = treeCount / 20f * (treeCount / 20f) * 2f;

        BLOCK_GRASS = m.GetBlockId("Grass");
        BLOCK_OAKTRUNK = m.GetBlockId("OakTreeTrunk");
        BLOCK_OAKLEAVES = m.GetBlockId("OakLeaves");
        BLOCK_APPLES = m.GetBlockId("Apples");
        BLOCK_SPRUCETRUNK = m.GetBlockId("SpruceTreeTrunk");
        BLOCK_SPRUCELEAVES = m.GetBlockId("SpruceLeaves");
        BLOCK_BIRCHTRUNK = m.GetBlockId("BirchTreeTrunk");
        BLOCK_BIRCHLEAVES = m.GetBlockId("BirchLeaves");
        modEvents.PopulateChunk += PopulateChunk;
    }

    private void PopulateChunk(PopulateChunkArgs args)
    {
        int x = args.X * m.GetChunkSize();
        int y = args.Y * m.GetChunkSize();
        int z = args.Z * m.GetChunkSize();

        //forests
        float count = treenoise.GetValue(x / 512f, 0f, y / 512f) * 1000f;
        {
            count = MathF.Min(count, 300f);
            MakeSmallTrees(x, y, z, m.GetChunkSize(), _rnd, (int)count);
        }

        //random trees
        MakeSmallTrees(x, y, z, m.GetChunkSize(), _rnd, treeCount + 10 - (10 - (treeCount / 10)));
    }

    private void MakeSmallTrees(int cx, int cy, int cz, int chunksize, Random rnd, int count)
    {
        int chooseTreeType;
        for (int i = 0; i < count; i++)
        {
            int x = cx + rnd.Next(chunksize);
            int y = cy + rnd.Next(chunksize);
            int z = cz + rnd.Next(chunksize);
            if (!m.IsValidPos(x, y, z) || m.GetBlock(x, y, z) != BLOCK_GRASS)
            {
                continue;
            }

            chooseTreeType = rnd.Next(0, 3);
            switch (chooseTreeType)
            {
                case 0: MakeTreeType1(x, y, z, rnd); break; //Spruce
                case 1: MakeTreeType2(x, y, z, rnd); break; //Oak
                case 2: MakeTreeType3(x, y, z, rnd); break; //Birch
            }

            ;
        }
    }

    private void MakeTreeType1(int x, int y, int z, Random rnd)
    {
        int treeHeight = rnd.Next(8, 12);
        int xx = 0;
        int yy = 0;
        int dir = 0;

        for (int i = 0; i < treeHeight; i++)
        {
            SetBlock(x, y, z + i, BLOCK_SPRUCETRUNK);
            if (i == treeHeight - 4)
            {
                SetBlock(x + 1, y, z + i, BLOCK_SPRUCETRUNK);
                SetBlock(x - 1, y, z + i, BLOCK_SPRUCETRUNK);
                SetBlock(x, y + 1, z + i, BLOCK_SPRUCETRUNK);
                SetBlock(x, y - 1, z + i, BLOCK_SPRUCETRUNK);
            }

            if (i == treeHeight - 3)
            {
                for (int j = 1; j < 9; j++)
                {
                    dir += 45;
                    for (int k = 1; k < 4; k++)
                    {
                        int length = dir % 90 == 0 ? k : k / 2;
                        xx = length * (int)System.Math.Round(System.Math.Cos(dir * System.Math.PI / 180));
                        yy = length * (int)System.Math.Round(System.Math.Sin(dir * System.Math.PI / 180));

                        SetBlock(x + xx, y + yy, z + i, BLOCK_SPRUCETRUNK);
                        SetBlockIfEmpty(x + xx, y + yy, z + i + 1, BLOCK_SPRUCELEAVES);

                        SetBlockIfEmpty(x + xx + 1, y + yy, z + i, BLOCK_SPRUCELEAVES);
                        SetBlockIfEmpty(x + xx - 1, y + yy, z + i, BLOCK_SPRUCELEAVES);
                        SetBlockIfEmpty(x + xx, y + yy + 1, z + i, BLOCK_SPRUCELEAVES);
                        SetBlockIfEmpty(x + xx, y + yy - 1, z + i, BLOCK_SPRUCELEAVES);
                    }
                }
            }

            if (i == treeHeight - 1)
            {
                for (int j = 1; j < 9; j++)
                {
                    dir += 45;
                    for (int k = 1; k < 3; k++)
                    {
                        int length = dir % 90 == 0 ? k : k / 2;
                        xx = length * (int)System.Math.Round(System.Math.Cos(dir * System.Math.PI / 180));
                        yy = length * (int)System.Math.Round(System.Math.Sin(dir * System.Math.PI / 180));

                        SetBlock(x + xx, y + yy, z + i, BLOCK_SPRUCETRUNK);
                        SetBlockIfEmpty(x + xx, y + yy, z + i + 1, BLOCK_SPRUCELEAVES);

                        SetBlockIfEmpty(x + xx + 1, y + yy, z + i, BLOCK_SPRUCELEAVES);
                        SetBlockIfEmpty(x + xx - 1, y + yy, z + i, BLOCK_SPRUCELEAVES);
                        SetBlockIfEmpty(x + xx, y + yy + 1, z + i, BLOCK_SPRUCELEAVES);
                        SetBlockIfEmpty(x + xx, y + yy - 1, z + i, BLOCK_SPRUCELEAVES);
                    }
                }
            }
        }
    }

    private void MakeTreeType2(int x, int y, int z, Random rnd)
    {
        int treeHeight = rnd.Next(4, 6);
        int xx = 0;
        int yy = 0;
        int dir = 0;
        float chanceToAppleTree = 0.1f;
        for (int i = 0; i < treeHeight; i++)
        {
            SetBlock(x, y, z + i, BLOCK_OAKTRUNK);
            if (i == treeHeight - 1)
            {
                for (int j = 1; j < 9; j++)
                {
                    dir += 45;
                    for (int k = 1; k < 2; k++)
                    {
                        int length = dir % 90 == 0 ? k : k / 2;
                        xx = length * (int)System.Math.Round(System.Math.Cos(dir * System.Math.PI / 180));
                        yy = length * (int)System.Math.Round(System.Math.Sin(dir * System.Math.PI / 180));

                        SetBlock(x + xx, y + yy, z + i, BLOCK_OAKTRUNK);
                        if (chanceToAppleTree < rnd.NextDouble())
                        {
                            SetBlockIfEmpty(x + xx, y + yy, z + i + 1, BLOCK_OAKLEAVES);
                            SetBlockIfEmpty(x + xx + 1, y + yy, z + i, BLOCK_OAKLEAVES);
                            SetBlockIfEmpty(x + xx - 1, y + yy, z + i, BLOCK_OAKLEAVES);
                            SetBlockIfEmpty(x + xx, y + yy + 1, z + i, BLOCK_OAKLEAVES);
                            SetBlockIfEmpty(x + xx, y + yy - 1, z + i, BLOCK_OAKLEAVES);
                        }
                        else
                        {
                            float appleChance = 0.4f;
                            int tile;
                            tile = rnd.NextDouble() < appleChance ? BLOCK_APPLES : BLOCK_OAKLEAVES;
                            SetBlockIfEmpty(x + xx, y + yy, z + i + 1, tile);
                            tile = rnd.NextDouble() < appleChance ? BLOCK_APPLES : BLOCK_OAKLEAVES;
                            SetBlockIfEmpty(x + xx + 1, y + yy, z + i, tile);
                            tile = rnd.NextDouble() < appleChance ? BLOCK_APPLES : BLOCK_OAKLEAVES;
                            SetBlockIfEmpty(x + xx - 1, y + yy, z + i, tile);
                            tile = rnd.NextDouble() < appleChance ? BLOCK_APPLES : BLOCK_OAKLEAVES;
                            SetBlockIfEmpty(x + xx, y + yy + 1, z + i, tile);
                            tile = rnd.NextDouble() < appleChance ? BLOCK_APPLES : BLOCK_OAKLEAVES;
                            SetBlockIfEmpty(x + xx, y + yy - 1, z + i, tile);
                        }
                    }
                }
            }
        }
    }

    private void MakeTreeType3(int x, int y, int z, Random rnd)
    {
        int treeHeight = rnd.Next(6, 9);
        int xx = 0;
        int yy = 0;
        int dir = 0;
        for (int i = 0; i < treeHeight; i++)
        {
            SetBlock(x, y, z + i, BLOCK_BIRCHTRUNK);
            if (i % 3 == 0 && i > 3)
            {
                for (int j = 1; j < 9; j++)
                {
                    dir += 45;
                    for (int k = 1; k < 2; k++)
                    {
                        int length = dir % 90 == 0 ? k : k / 2;
                        xx = length * (int)System.Math.Round(System.Math.Cos(dir * System.Math.PI / 180));
                        yy = length * (int)System.Math.Round(System.Math.Sin(dir * System.Math.PI / 180));

                        SetBlock(x + xx, y + yy, z + i, BLOCK_BIRCHTRUNK);
                        SetBlockIfEmpty(x + xx, y + yy, z + i + 1, BLOCK_BIRCHLEAVES);

                        SetBlockIfEmpty(x + xx + 1, y + yy, z + i, BLOCK_BIRCHLEAVES);
                        SetBlockIfEmpty(x + xx - 1, y + yy, z + i, BLOCK_BIRCHLEAVES);
                        SetBlockIfEmpty(x + xx, y + yy + 1, z + i, BLOCK_BIRCHLEAVES);
                        SetBlockIfEmpty(x + xx, y + yy - 1, z + i, BLOCK_BIRCHLEAVES);
                    }
                }
            }

            if (i % 3 == 2 && i > 3)
            {
                dir = 45;
                for (int j = 1; j < 9; j++)
                {
                    dir += 45;
                    for (int k = 1; k < 3; k++)
                    {
                        int length = dir % 90 == 0 ? k : k / 2;
                        xx = length * (int)System.Math.Round(System.Math.Cos(dir * System.Math.PI / 180));
                        yy = length * (int)System.Math.Round(System.Math.Sin(dir * System.Math.PI / 180));

                        SetBlock(x + xx, y + yy, z + i, BLOCK_BIRCHTRUNK);
                        SetBlockIfEmpty(x + xx, y + yy, z + i + 1, BLOCK_BIRCHLEAVES);

                        SetBlockIfEmpty(x + xx + 1, y + yy, z + i, BLOCK_BIRCHLEAVES);
                        SetBlockIfEmpty(x + xx - 1, y + yy, z + i, BLOCK_BIRCHLEAVES);
                        SetBlockIfEmpty(x + xx, y + yy + 1, z + i, BLOCK_BIRCHLEAVES);
                        SetBlockIfEmpty(x + xx, y + yy - 1, z + i, BLOCK_BIRCHLEAVES);
                    }
                }
            }

            SetBlockIfEmpty(x, y, z + treeHeight, BLOCK_BIRCHLEAVES);
        }
    }

    private void SetBlock(int x, int y, int z, int blocktype)
    {
        if (m.IsValidPos(x, y, z))
        {
            m.SetBlock(x, y, z, blocktype);
        }
    }

    private void SetBlockIfEmpty(int x, int y, int z, int blocktype)
    {
        if (m.IsValidPos(x, y, z) && m.GetBlock(x, y, z) == 0)
        {
            m.SetBlock(x, y, z, blocktype);
        }
    }
}
