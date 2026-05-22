namespace MeinKraft.Mods;

public class Sign : IMod
{
    private IServerModManager? m;

    public void PreStart(IServerModManager m) => m.RequireMod("CoreBlocks");

    public void Start(IServerModManager manager, IModEvents modEvents)
    {
        m = manager;
        m.SetBlockType(154, "Sign", new BlockType()
        {
            AllTextures = "Sign",
            DrawType = DrawType.Solid,
            WalkableType = WalkableType.Solid,
            IsUsable = true,
            IsTool = true,
        });
        m.AddToCreativeInventory("Sign");
        m.SetBlockType(155, "PermissionSign", new BlockType()
        {
            AllTextures = "PermissionSign",
            DrawType = DrawType.Solid,
            WalkableType = WalkableType.Solid,
            IsUsable = true,
            IsTool = true,
        });
        m.AddToCreativeInventory("PermissionSign");
    }

}