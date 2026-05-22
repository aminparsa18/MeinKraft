namespace MeinKraft.Mods;

public class Revert : IMod
{
    private IServerModManager m;
    private List<object[]> lines = [];

    public int MaxRevert = 2000;


    public void PreStart(IServerModManager m) => m.RequireMod("BuildLog");

    public void Start(IServerModManager manager, IModEvents modEvents)
    {
        m = manager;
        m.RegisterPrivilege("revert");
        m.RegisterCommandHelp("revert", "/revert [playername] [number of changes]");
        lines = (List<object[]>)m.GetGlobalDataNotSaved("LogLines");
        modEvents.Command += OnCommand;
    }

    private void OnCommand(CommandArgs args)
    {
        if (!args.Command.Equals("revert", StringComparison.InvariantCultureIgnoreCase))
            return;

        if (!m.PlayerHasPrivilege(args.Player, "revert"))
        {
            m.SendMessage(args.Player, $"{GameConstants.colorError}Insufficient privileges to use revert.");
            args.Handled = true;
            return;
        }

        string targetplayername;
        int n;
        try
        {
            string[] argsSplit = args.Argument.Split(' ');
            targetplayername = argsSplit[0];
            n = int.Parse(argsSplit[1]);
        }
        catch
        {
            m.SendMessage(args.Player, $"{GameConstants.colorError}Invalid arguments. Type /help to see command's usage.");
            return;
        }

        if (n > MaxRevert)
            m.SendMessage(args.Player, $"{GameConstants.colorError}Can't revert more than {MaxRevert} block changes");

        int reverted = 0;
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            object[] l = lines[i];
            string lplayername = (string)l[6];
            int lx = (short)l[1];
            int ly = (short)l[2];
            int lz = (short)l[3];
            bool lbuild = (bool)l[5];
            short lblocktype = (short)l[4];
            if (lplayername.Equals(targetplayername, StringComparison.InvariantCultureIgnoreCase))
            {
                if (lbuild)
                    m.SetBlock(lx, ly, lz, 0);
                else
                    m.SetBlock(lx, ly, lz, lblocktype);

                reverted++;
                if (reverted >= n)
                    break;
            }
        }

        if (reverted == 0)
            m.SendMessage(args.Player, string.Format(GameConstants.colorError + "Not reverted any block changes by player {0}.", targetplayername));
        else
        {
            m.SendMessageToAll(string.Format("{0} reverted {1} block changes by player {2}", m.GetPlayerName(args.Player), reverted, targetplayername));
            m.LogServerEvent(string.Format("{0} reverts {1} block changes by player {2}", m.GetPlayerName(args.Player), reverted, targetplayername));
        }

        args.Handled = true;
    }
}
