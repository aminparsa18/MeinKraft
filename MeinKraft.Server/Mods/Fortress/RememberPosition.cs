namespace MeinKraft.Mods;

public class RememberPosition : IMod
{
    public void PreStart(IServerModManager m) { }

    public void Start(IServerModManager manager, IModEvents modEvents)
    {
        m = manager;
        LoadData();
        m.RegisterOnSave(SaveData);
        modEvents.PlayerJoin += OnJoin;
        modEvents.PlayerLeave += OnLeave;
    }

    private IServerModManager m;
    public PositionStorage? positions;

    // Resolve path relative to exe, not working directory
    private static string Filename => Path.Combine(
        AppContext.BaseDirectory, "UserData", "StoredPositions.txt");

    public void LoadData()
    {
        positions = new PositionStorage();

        if (!File.Exists(Filename))
        {
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(Filename);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] linesplit = lines[i].Split(';');
                try
                {
                    int[] pos = PositionStorage.StringToPos(linesplit[1]);
                    positions.Store(linesplit[0], pos[0], pos[1], pos[2]);
                }
                catch
                {
                    Console.WriteLine("[WARNING] Skipping invalid entry on line {0}.", i + 1);
                }
            }
        }
        catch
        {
            Console.WriteLine("[ERROR] StoredPositions.txt could not be read!");
        }
    }

    public void SaveData()
    {
        try
        {
            // Ensure directory exists before writing
            Directory.CreateDirectory(Path.GetDirectoryName(Filename)!);

            List<string> lines = [];
            foreach (UserEntry entry in positions.PlayerPositions)
            {
                lines.Add(string.Format("{0};{1}", entry.Name, entry.Position));
            }

            File.WriteAllLines(Filename, lines.ToArray());
        }
        catch
        {
            Console.WriteLine("[ERROR] Could not save last player positions");
        }
    }

    public void OnJoin(PlayerJoinArgs args)
    {
        string name = m.GetPlayerName(args.Player);
        if (positions.IsStoredAt(name) != -1)
        {
            int[] pos = positions.Get(name);
            if (pos != null)
            {
                m.SetPlayerPosition(args.Player, pos[0], pos[1], pos[2]);
                //Console.WriteLine("[INFO] Position restored: {0}({1},{2},{3})", name, pos[0], pos[1], pos[2]);
            }
        }
    }

    public void OnLeave(PlayerLeaveArgs args)
    {
        if (m.IsBot(args.Player))
        {
            //Don't store bot positions
            return;
        }

        //Do not save position if it is outside the map
        int x = (int)m.GetPlayerPositionX(args.Player);
        int y = (int)m.GetPlayerPositionY(args.Player);
        int z = (int)m.GetPlayerPositionZ(args.Player);
        if (x > 0 && y > 0 && z > 0)
        {
            if (x < m.GetMapSizeX() && y < m.GetMapSizeY() && z < m.GetMapSizeZ())
            {
                positions.Store(m.GetPlayerName(args.Player), x, y, z);
            }
        }
    }
}

public class PositionStorage
{
    public List<UserEntry> PlayerPositions { get; set; }

    public PositionStorage()
    {
        PlayerPositions = [];
    }

    public int IsStoredAt(string player)
    {
        for (int i = 0; i < PlayerPositions.Count; i++)
        {
            if (player.Equals(PlayerPositions[i].Name, StringComparison.InvariantCultureIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    public void Store(string player, int x, int y, int z)
    {
        if (IsStoredAt(player) != -1)
        {
            Delete(player);
        }

        UserEntry entry = new()
        {
            Name = player,
            Position = PosToString(x, y, z)
        };
        PlayerPositions.Add(entry);
        //Console.WriteLine("[INFO] Position saved: {0}({1})", entry.Name, entry.Position);
    }

    public void Delete(string player)
    {
        for (int i = 0; i < PlayerPositions.Count; i++)
        {
            if (player.Equals(PlayerPositions[i].Name, StringComparison.InvariantCultureIgnoreCase))
            {
                PlayerPositions.RemoveAt(i);
                return;
            }
        }
    }

    public int[] Get(string player)
    {
        int index = IsStoredAt(player);
        if (index != -1)
        {
            return StringToPos(PlayerPositions[index].Position);
        }

        return null;
    }

    public static string PosToString(int x, int y, int z) => string.Format("{0},{1},{2}", x, y, z);

    public static int[] StringToPos(string position)
    {
        try
        {
            string[] split = position.Split(',');
            int[] retval = [int.Parse(split[0]), int.Parse(split[1]), int.Parse(split[2])];
            return retval;
        }
        catch
        {
            Console.WriteLine("[ERROR] Could not convert '{0}' to coordinates", position);
            return null;
        }
    }
}

public class UserEntry
{
    public string Name { get; set; }
    public string Position { get; set; }
}
