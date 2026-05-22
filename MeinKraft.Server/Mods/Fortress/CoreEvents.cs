namespace MeinKraft.Mods;

/// <summary>
/// This class contains all command logic and event handling
/// </summary>
public class CoreEvents : IMod
{
    private IServerModManager m;

    public void PreStart(IServerModManager m) => m.RequireMod("CoreBlocks");
    public void Start(IServerModManager manager, IModEvents modEvents)
    {
        m = manager;

        modEvents.Command += OnCommandSetModel;
        modEvents.SpecialKey += OnRespawnKey;
        modEvents.SpecialKey += OnSetSpawnKey;
        modEvents.PlayerDeath += OnPlayerDeath;
    }

    //Dictionary to store temporary spawn positions
    private readonly Dictionary<string, float[]> spawnPositions = [];

    private void OnSetSpawnKey(SpecialKeyArgs args)
    {
        if (args.Key != SpecialKey.SetSpawn)
            return;

        float[] pos = [m.GetPlayerPositionX(args.Player), m.GetPlayerPositionY(args.Player), m.GetPlayerPositionZ(args.Player)];
        spawnPositions[m.GetPlayerName(args.Player)] = pos;
        m.SendMessage(args.Player, "&7Spawn position set");
    }

    private void OnRespawnKey(SpecialKeyArgs args)
    {
        if (args.Key != SpecialKey.Respawn)
            return;

        Respawn(args.Player);
        m.SendMessage(args.Player, "&7Respawn");
    }

    private string ColoredPlayername(int player)
        //Returns the player name in group color
        => string.Format("{0}{1}", m.GetGroupColor(player), m.GetPlayerName(player));

    private void OnPlayerDeath(PlayerDeathArgs args)
    {
        //Respawn the player and send a death message to all players
        Respawn(args.Player);

        string deathMessage;
        //Different death message depending on reason for death
        switch (args.Reason)
        {
            case DeathReason.FallDamage:
                deathMessage = string.Format("{0} &7was doomed to fall.", ColoredPlayername(args.Player));
                break;
            case DeathReason.BlockDamage:
                if (args.SourceId == m.GetBlockId("Lava"))
                    deathMessage = string.Format("{0} &7thought they could swim in Lava.", ColoredPlayername(args.Player));
                else if (args.SourceId == m.GetBlockId("Fire"))
                    deathMessage = string.Format("{0} &7was burned alive.", ColoredPlayername(args.Player));
                else
                    deathMessage = string.Format("{0} &7was killed by {1}.", ColoredPlayername(args.Player), m.GetBlockName(args.SourceId));
                break;
            case DeathReason.Drowning:
                deathMessage = string.Format("{0} &7tried to breathe under water.", ColoredPlayername(args.Player));
                break;
            case DeathReason.Explosion:
                deathMessage = string.Format("{0} &7was blown into pieces by {1}.", ColoredPlayername(args.Player), ColoredPlayername(args.SourceId));
                break;
            default:
                deathMessage = string.Format("{0} &7died.", ColoredPlayername(args.Player));
                break;
        }

        m.SendMessageToAll(deathMessage);
    }

    private void Respawn(int player)
    {
        if (!spawnPositions.ContainsKey(m.GetPlayerName(player)))
        {
            float[] pos = m.GetDefaultSpawnPosition(player);
            m.SetPlayerPosition(player, pos[0], pos[1], pos[2]);
        }
        else
        {
            float[] pos = spawnPositions[m.GetPlayerName(player)];
            m.SetPlayerPosition(player, pos[0], pos[1], pos[2]);
        }
    }

    private void OnCommandSetModel(CommandArgs args)
    {
        if (!args.Command.Equals("setmodel", StringComparison.InvariantCultureIgnoreCase))
            return;

        if (!m.PlayerHasPrivilege(args.Player, "setmodel"))
        {
            m.SendMessage(args.Player, GameConstants.colorError + "No setmodel privilege");
            args.Handled = true;
            return;
        }

        string[] ss = args.Argument.Split(' ');
        string targetplayername = ss[0];
        string modelname = ss[1];
        string texturename = null;
        if (ss.Length >= 3)
            texturename = ss[2];

        bool found = false;
        foreach (int p in m.AllPlayers())
        {
            if (m.GetPlayerName(p).Equals(targetplayername, StringComparison.InvariantCultureIgnoreCase))
            {
                m.SetPlayerModel(p, modelname, texturename);
                found = true;
            }
        }

        if (!found)
            m.SendMessage(args.Player, GameConstants.colorError + string.Format("Player {0} not found", targetplayername));

        args.Handled = true;
    }
}
