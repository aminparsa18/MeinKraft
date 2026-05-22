namespace MeinKraft.Mods;

//for debugging
public class Ghost : IMod
{
    private readonly bool enabled = false;
    private readonly List<Pos> history = [];
    private IServerModManager m;
    private int ghost;

    public void PreStart(IServerModManager m) { }

    public void Start(IServerModManager manager, IModEvents modEvents)
    {
        m = manager;
        if (enabled)
        {
            modEvents.LoadWorld += OnLoad;
        }
    }

    private void OnLoad(LoadWorldArgs args)
    {
        m.RegisterTimer(F, 0.1);
        ghost = m.AddBot("Ghost");
    }

    private class Pos
    {
        public float x;
        public float y;
        public float z;
        public int heading;
        public int pitch;
    }

    private void F()
    {
        int[] clients = m.AllPlayers();
        foreach (int p in clients)
        {
            if (p == ghost)
            {
                continue;
            }

            Pos pos = new()
            {
                x = m.GetPlayerPositionX(p),
                y = m.GetPlayerPositionY(p),
                z = m.GetPlayerPositionZ(p),

                heading = m.GetPlayerHeading(p),
                pitch = m.GetPlayerPitch(p)
            };
            history.Add(pos);
        }

        if (history.Count < 20)
        {
            return;
        }

        Pos p1 = history[0];
        history.RemoveAt(0);
        m.SetPlayerPosition(ghost, p1.x, p1.y, p1.z);
        m.SetPlayerOrientation(ghost, p1.heading, p1.pitch, 0);
    }
}
