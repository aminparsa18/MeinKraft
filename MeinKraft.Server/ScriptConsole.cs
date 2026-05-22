// Copyright (c) 2011 by Henon <meinrad.recheis@gmail.com>
using MeinKraft;
using OpenTK.Mathematics;
using static MeinKraft.ServerPacketService;

public class ScriptConsole
{
    public ScriptConsole(ServerGameService s, IBlockRegistry blockRegistry, ISaveGameService saveGameService, IClientRegistry serverClientService, IServerPacketService serverPacketService, int client_id)
    {
        m_server = s;
        _blockRegistry = blockRegistry;
        _saveGameService = saveGameService;
        _serverClientService = serverClientService;
        _serverPacketService = serverPacketService;
        m_client = client_id;
    }

    public void InjectConsoleCommands(IScriptInterpreter interpreter)
    {
        interpreter.SetFunction("out", new Action<object>(Print));
        interpreter.SetFunction("materials", new Action(PrintMaterials));
        interpreter.SetFunction("materials_between", new Action<double, double>(PrintMaterials));
        interpreter.SetFunction("find_material", new Action<string>(FindMaterial));
        interpreter.SetFunction("position", new Action(PrintPosition));
        interpreter.SetFunction("get_position", new Func<Vector3i>(GetPosition));
        interpreter.SetVariable("turtle", Turtle);
        interpreter.SetFunction("set_block", new Action<double, double, double, double>(SetBlock));
        interpreter.SetFunction("get_block", new Func<double, double, double, int>(GetBlock));
        interpreter.SetFunction("get_height", new Func<double, double, double>(GetHeight));
        interpreter.SetFunction("get_mapsize", new Func<int[]>(GetMapSize));
        interpreter.SetFunction("set_chunk", new Action<double, double, double, ushort[]>(SetChunk));
        interpreter.SetFunction("set_chunks", new Action<Dictionary<Vector3i, ushort[]>>(SetChunks));
        interpreter.SetFunction("set_chunks_offset", new Action<double, double, double, Dictionary<Vector3i, ushort[]>>(SetChunks));
        interpreter.SetFunction("get_chunk", new Func<double, double, double, ushort[]>(GetChunk));
        interpreter.SetFunction("get_chunks_from_database", new Func<double, double, double, double, double, double, string, Dictionary<Vector3i, ushort[]>>(GetChunksFromDatabase));
        interpreter.SetFunction("copy_chunks_to_database", new Action<double, double, double, double, double, double, string>(CopyChunksToDatabase));
        interpreter.SetFunction("delete_chunk", new Action<double, double, double>(DeleteChunk));
        interpreter.SetFunction("delete_chunk_range", new Action<double, double, double, double, double, double>(DeleteChunkRange));
        interpreter.SetFunction("backup_database", new Action<string>(BackupDatabase));
        interpreter.SetFunction("clear", new Action(Clear));
    }

    private readonly IBlockRegistry _blockRegistry;
    private readonly ISaveGameService _saveGameService;
    private readonly IClientRegistry _serverClientService;
    private readonly IServerPacketService _serverPacketService;
    private readonly ServerGameService m_server;
    private readonly int m_client;

    public void Print(object obj)
    {
        if (obj == null)
        {
            return;
        }

        _serverPacketService.SendMessage(m_client, obj.ToString(), MessageType.Normal);
    }

    public void PrintMaterials() => PrintMaterials(0, GameConstants.MAX_BLOCKTYPES);

    public void PrintMaterials(double start, double end)
    {
        for (int i = (int)start; i < end; i++)
        {
            Print(string.Format("{0}: {1}", i, _blockRegistry.BlockTypes[i].Name));
        }
    }

    public void FindMaterial(string search_string)
    {
        for (int i = 0; i < GameConstants.MAX_BLOCKTYPES; i++)
        {
            if (_blockRegistry.BlockTypes[i].Name.Contains(search_string))
            {
                Print(string.Format("{0}: {1}", i, _blockRegistry.BlockTypes[i].Name));
            }
        }
    }

    public void PrintPosition()
    {
        ServerPlayer client = _serverClientService.GetClient(m_client);
        Vector3i pos = GetPosition();
        Print(string.Format("Position: X {0}, Y {1}, Z{2}", pos.X, pos.Y, pos.Z));
    }

    public Vector3i GetPosition()
    {
        ServerPlayer client = _serverClientService.GetClient(m_client);
        return m_server.PlayerBlockPosition(client);
    }

    public void SetBlock(double x, double y, double z, double material)
        //m_server.CreateBlock((int)x, (int)y, (int)z, m_client, new Item() { BlockId = (int)material, ItemClass = ItemClass.Block, BlockCount = 1 });
        => m_server.SetBlock((int)x, (int)y, (int)z, (int)material);

    public int GetBlock(double x, double y, double z) => m_server.GetBlock((int)x, (int)y, (int)z);

    public double GetHeight(double x, double y) => m_server.GetHeight((int)x, (int)y);

    public void DeleteChunk(double x, double y, double z) => m_server.DeleteChunk((int)x, (int)y, (int)z);

    public void DeleteChunkRange(double x1, double y1, double z1, double x2, double y2, double z2)
    {
        List<Vector3i> chunkPositions = [];
        int chunksize = GameConstants.ServerChunkSize;
        for (int x = (int)x1; x < (int)x2; x += chunksize)
        {
            for (int y = (int)y1; y < (int)y2; y += chunksize)
            {
                for (int z = (int)z1; z < (int)z2; z += chunksize)
                {
                    chunkPositions.Add(new Vector3i() { X = x, Y = y, Z = z });
                }
            }
        }

        m_server.DeleteChunks(chunkPositions);
    }

    public void SetChunk(double x, double y, double z, ushort[] data) => m_server.SetChunk((int)x, (int)y, (int)z, data);

    public void SetChunks(Dictionary<Vector3i, ushort[]> chunks) => m_server.SetChunks(chunks);

    public void SetChunks(double offsetX, double offsetY, double offsetZ, Dictionary<Vector3i, ushort[]> chunks) => m_server.SetChunks((int)offsetX, (int)offsetY, (int)offsetZ, chunks);

    public ushort[] GetChunk(double x, double y, double z) => m_server.GetChunk((int)x, (int)y, (int)z);

    public ushort[] GetChunkFromDatabase(double x, double y, double z, string file) => m_server.GetChunkFromDatabase((int)x, (int)y, (int)z, file);

    public Dictionary<Vector3i, ushort[]> GetChunksFromDatabase(double x1, double y1, double z1, double x2, double y2, double z2, string file)
    {
        List<Vector3i> chunkPositions = [];
        int chunksize = GameConstants.ServerChunkSize;
        for (int x = (int)x1; x < (int)x2; x += chunksize)
        {
            for (int y = (int)y1; y < (int)y2; y += chunksize)
            {
                for (int z = (int)z1; z < (int)z2; z += chunksize)
                {
                    chunkPositions.Add(new Vector3i() { X = x / chunksize, Y = y / chunksize, Z = z / chunksize });
                }
            }
        }

        Dictionary<Vector3i, ushort[]> chunks = m_server.GetChunksFromDatabase(chunkPositions, file);
        Print(chunks.Count + " chunks loaded.");
        return chunks;
    }

    public void CopyChunksToDatabase(double x1, double y1, double z1, double x2, double y2, double z2, string file)
    {
        List<Vector3i> chunkPositions = [];
        int chunksize = GameConstants.ServerChunkSize;
        for (int x = (int)x1; x < (int)x2; x += chunksize)
        {
            for (int y = (int)y1; y < (int)y2; y += chunksize)
            {
                for (int z = (int)z1; z < (int)z2; z += chunksize)
                {
                    chunkPositions.Add(new Vector3i() { X = x, Y = y, Z = z });
                }
            }
        }

        m_server.SaveChunksToDatabase(chunkPositions, file);
    }

    public void BackupDatabase(string backupFilename) => _saveGameService.BackupDatabase(backupFilename);

    public int[] GetMapSize() => m_server.GetMapSize();

    public void Clear() => m_server.ClearInterpreter(m_client);

    private Turtle m_turtle;

    public Turtle Turtle
    {
        get
        {
            if (m_turtle == null)
            {
                m_turtle = new Turtle { Console = this };
            }

            return m_turtle;
        }
    }
}

public class Turtle
{

    public ScriptConsole Console;
    //public enum Orientation
    //{
    //   North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest,
    //   Up, UpNorth, UpNorthEast, UpEast, UpSouthEast, UpSouth, UpSouthWest, UpWest, UpNorthWest,
    //   Down, DownNorth, DownNorthEast, DownEast, DownSouthEast, DownSouth, DownSouthWest, DownWest, DownNorthWest,
    //}

    public Vector3i position = new(0, 0, 0);

    public double x { get { return position.X; } }

    public double y { get { return position.Y; } }

    public double z { get { return position.Z; } }

    //public Orientation orientation;

    public void set_player_position() => position = Console.GetPosition();

    public double material = 0;

    public void Put() => Console.SetBlock(x, y, z, material);

    public Vector3i direction = new(0, -1, 0); // turtle looks north by default

    public void Look_north() => direction = new Vector3i(0, -1, 0);

    public void look_east() => direction = new Vector3i(-1, 0, 0);

    public void look_south() => direction = new Vector3i(0, 1, 0);

    public void look_west() => direction = new Vector3i(1, 0, 0);

    public void look_up() => direction = new Vector3i(0, 0, 1);

    public void look_down() => direction = new Vector3i(0, 0, -1);

    public void forward() => position = new Vector3i(position.X + direction.X, position.Y + direction.Y, position.Z + direction.Z);

    public void back() => position = new Vector3i(position.X - direction.X, position.Y - direction.Y, position.Z - direction.Z);

    public void save() => m_stack.Push([position, direction]); // push the current turtle position and direction to the stack

    public void load() // pop position and direction from the stack and set them
    {
        Vector3i[] array = m_stack.Pop();
        if (array == null)
        {
            return;
        }

        position = array[0];
        direction = array[1];
    }

    private readonly Stack<Vector3i[]> m_stack = new();

    public void status()
    {
        Console.Print("Turtle status:");
        Console.Print("Position: " + position);
        Console.Print("Orientation: " + DirectionToString(direction) + "  " + direction);
    }

    public static string DirectionToString(Vector3i dir)
    {
        if (dir.X == 0)
        {
            if (dir.Y == 0)
            {
                if (dir.Z == 1)
                {
                    return "Up";
                }
                else if (dir.Z == -1)
                {
                    return "Down";
                }
            }

            if (dir.Z == 0)
            {
                if (dir.Y == 1)
                {
                    return "South";
                }
                else if (dir.Y == -1)
                {
                    return "North";
                }
            }
        }
        else if (dir.Y == 0 && dir.Z == 0)
        {
            if (dir.X == 1)
            {
                return "West";
            }
            else if (dir.X == -1)
            {
                return "East";
            }
        }

        return "Unknown direction";
    }

    //public void turn_right()
    //{

    //}


    //public void turn_left()
    //{

    //}

}
