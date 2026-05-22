namespace MeinKraft.Mods;

public class SandPhysics : IMod
{
    private IServerModManager m;

    public void PreStart(IServerModManager m) => m.RequireMod("CoreBlocks");

    public void Start(IServerModManager manager, IModEvents modEvents)
    {
        m = manager;
        modEvents.BlockBuild += Build;
        modEvents.BlockDelete += Delete;
    }

    private void Build(BlockBuildArgs args) => Update(args.X, args.Y, args.Z);
    private void Delete(BlockDeleteArgs args) => Update(args.X, args.Y, args.Z);

    private void Update(int x, int y, int z)
    {
        if (IsValidDualPos(x, y, z - 1) && (IsSlideDown(x, y, z, m.GetBlockId("Sand")) || IsSlideDown(x, y, z, m.GetBlockId("Gravel"))))
        {
            BlockMoveDown(x, y, z - 1, 0);
            Update(x, y, z - 1);
        }
        else if (IsValidDualPos(x, y, z) && (IsDestroyOfBase(x, y, z, m.GetBlockId("Sand")) || IsDestroyOfBase(x, y, z, m.GetBlockId("Gravel"))))
        {
            BlockMoveDown(x, y, z, GetDepth(x, y, z));
            Update(x, y, z + 1);
        }
    }

    private int GetDepth(int x, int y, int z)
    {
        int startHeight = z;
        while (m.IsValidPos(x, y, z) && IsSoftBlock(m.GetBlock(x, y, z)))
        {
            z--;
        }

        return startHeight - z - 1;
    }

    private bool IsSoftBlock(int blockType)
    {
        if (blockType == 0)
        {
            return true;
        }
        else if (m.IsBlockFluid(blockType))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private bool IsSlideDown(int x, int y, int z, int blockType) => IsSoftBlock(m.GetBlock(x, y, z - 1)) && (m.GetBlock(x, y, z) == blockType);

    private void BlockMoveDown(int x, int y, int z, int depth)
    {
        m.SetBlock(x, y, z - depth, m.GetBlock(x, y, z + 1));
        m.SetBlock(x, y, z + 1, 0);
    }

    private bool IsDestroyOfBase(int x, int y, int z, int blockType) => IsSoftBlock(m.GetBlock(x, y, z)) && (m.GetBlock(x, y, z + 1) == blockType);

    private bool IsValidDualPos(int x, int y, int z) => m.IsValidPos(x, y, z) && m.IsValidPos(x, y, z + 1);
}
