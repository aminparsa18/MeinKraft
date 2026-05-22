namespace MeinKraft.Mods;

public class VandalFinder : IMod
{
    private IServerModManager m;
    private List<object[]> lines = [];

    public void PreStart(IServerModManager m)
    {
        m.RequireMod("CoreBlocks");
        m.RequireMod("BuildLog");
    }

    public void Start(IServerModManager manager, IModEvents modEvents)
    {
        m = manager;
        m.SetBlockType("VandalFinder", new BlockType()
        {
            AllTextures = "VandalFinder",
            DrawType = DrawType.Solid,
            WalkableType = WalkableType.Solid,
            IsUsable = true,
            IsTool = true,
        });
        m.AddToCreativeInventory("VandalFinder");
        modEvents.BlockUseWithTool += OnUseWithTool;
        lines = (List<object[]>)m.GetGlobalDataNotSaved("LogLines");
    }

    private void OnUseWithTool(BlockUseWithToolArgs args)
    {
        if (m.GetBlockName(args.Tool) == "VandalFinder")
            ShowBlockLog(args.Player, args.X, args.Y, args.Z);
    }

    private void ShowBlockLog(int player, int x, int y, int z)
    {
        List<string> messages = [];
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            object[] l = lines[i];
            int lx = (short)l[1];
            int ly = (short)l[2];
            int lz = (short)l[3];
            DateTime ltimestamp = (DateTime)l[0];
            string lplayername = (string)l[6];
            int lblocktype = (short)l[4];
            bool lbuild = (bool)l[5];
            if (lx == x && ly == y && lz == z)
            {
                messages.Add(string.Format("{0} {1} {2} {3}", ltimestamp.ToString(), lplayername, m.GetBlockName(lblocktype), lbuild ? "build" : "delete"));
                if (messages.Count > 10)
                {
                    return;
                }
            }
        }

        messages.Reverse();
        for (int i = 0; i < messages.Count; i++)
        {
            m.SendMessage(player, messages[i]);
        }

        if (messages.Count == 0)
        {
            m.SendMessage(player, "Block was never changed.");
        }
    }
}
