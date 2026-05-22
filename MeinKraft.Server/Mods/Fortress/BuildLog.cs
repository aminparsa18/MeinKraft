namespace MeinKraft.Mods;

public class BuildLog : IMod
{
    private IServerModManager? m;
    //can't pass LogLine object between mods. Store object as an array of fields instead.
    private readonly List<object[]> lines = [];

    public int MaxEntries = 50 * 1000;

    public void PreStart(IServerModManager m) => m.RequireMod("CoreBlocks");

    public void Start(IServerModManager manager, IModEvents modEvents)
    {
        m = manager;
        modEvents.BlockBuild += OnBuild;
        modEvents.BlockDelete += OnDelete;
        m.RegisterOnLoad(OnLoad);
        m.RegisterOnSave(OnSave);
        m.SetGlobalDataNotSaved("LogLines", lines);
    }

    private void OnLoad()
    {
        byte[] b = m.GetGlobalData("BuildLog");
        if (b != null)
        {
            MemoryStream ms = new(b);
            BinaryReader br = new(ms);
            int count = br.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var l = new object[8];
                l[0] = new DateTime(br.ReadInt64());//timestamp
                l[1] = br.ReadInt16();//x
                l[2] = br.ReadInt16();//y
                l[3] = br.ReadInt16();//z
                l[4] = br.ReadInt16();//blocktype
                l[5] = br.ReadBoolean();//build
                l[6] = br.ReadString();//playername
                l[7] = br.ReadString();//ip
                lines.Add(l);
            }
        }
    }

    private void OnSave()
    {
        MemoryStream ms = new();
        BinaryWriter bw = new(ms);
        bw.Write(lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            object[] l = lines[i];
            bw.Write(((DateTime)l[0]).Ticks);//timestamp
            bw.Write((short)l[1]);//x
            bw.Write((short)l[2]);//y
            bw.Write((short)l[3]);//z
            bw.Write((short)l[4]);//blocktype
            bw.Write((bool)l[5]);//build
            bw.Write((string)l[6]);//playername
            bw.Write((string)l[7]);//ip
        }

        m.SetGlobalData("BuildLog", ms.ToArray());
    }

    private void OnBuild(BlockBuildArgs args)
    {
        lines.Add(
        [
            DateTime.UtcNow,                        //timestamp
        (short)args.X,                          //x
        (short)args.Y,                          //y
        (short)args.Z,                          //z
        (short)m.GetBlock(args.X, args.Y, args.Z), //blocktype
        true,                                   //build
        m.GetPlayerName(args.Player),
        m.GetPlayerIp(args.Player),             //ip
    ]);
        if (lines.Count > MaxEntries)
            lines.RemoveRange(0, 1000);
    }

    private void OnDelete(BlockDeleteArgs args)
    {
        lines.Add(
        [
            DateTime.UtcNow,                //timestamp
        (short)args.X,                  //x
        (short)args.Y,                  //y
        (short)args.Z,                  //z
        (short)args.OldBlock,           //blocktype
        false,                          //build
        m.GetPlayerName(args.Player),   //playername
        m.GetPlayerIp(args.Player),     //ip
    ]);
        if (lines.Count > MaxEntries)
            lines.RemoveRange(0, 1000);
    }
}
