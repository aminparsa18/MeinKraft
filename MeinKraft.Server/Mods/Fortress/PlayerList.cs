using System.Drawing;

namespace MeinKraft.Mods.Fortress;

public class PlayerList : IMod
{
    public void PreStart(IServerModManager m) { }

    public void Start(IServerModManager manager, IModEvents modEvents)
    {
        m = manager;
        modEvents.SpecialKey += OnTabKey;
        modEvents.DialogClick += OnTabResponse;
        m.RegisterTimer(UpdateTab, 1);
    }

    private IServerModManager m;
    
    public string GetPrefix(int playerID) => $"[{m.GetGroupColor(playerID)}{m.GetGroupName(playerID)}&0] ";

    private void OnTabKey(SpecialKeyArgs args)
    {
        if (args.Key != SpecialKey.TabPlayerList)
        {
            return;
        }

        tabOpen[m.GetPlayerName(args.Player)] = true;
        Dialog d = new()
        {
            IsModal = true
        };
        List<Widget> widgets = [];

        // table alignment
        float tableX = Xcenter(m.GetScreenResolution(args.Player)[0], tableWidth);
        float tableY = tableMarginTop;

        // text to draw
        string row1 = m.ServerName;
        row1 = CutText(row1, HeadingFont, tableWidth - (2 * tablePadding));

        string row2 = m.ServerMotd;
        row2 = CutText(row2, SmallFontBold, tableWidth - (2 * tablePadding));


        string row3_1 = $"IP: {m.ServerIp}:{m.ServerPort}";
        string row3_2 = (int)(m.GetPlayerPing(args.Player) * 1000) + "ms";

        string row4_1 = $"Players: {m.AllPlayers().Length}";
        string row4_2 = $"Page: {page + 1}/{pageCount + 1}";

        string row5_1 = "ID";
        string row5_2 = "Player";
        string row5_3 = "Ping";

        // row heights
        float row1Height = TextHeight(row1, HeadingFont) + (2 * tablePadding);
        float row2Height = TextHeight(row2, SmallFontBold) + (2 * tablePadding);
        float row3Height = TextHeight(row3_1, SmallFont) + (2 * tablePadding);
        float row4Height = TextHeight(row4_1, SmallFont) + (2 * tablePadding);
        float row5Height = TextHeight(row5_1, NormalFontBold) + (2 * tablePadding);
        float listEntryHeight = TextHeight("Player", NormalFont) + (2 * listEntryPaddingTopBottom);

        float heightOffset = 0;

        // determine how many entries can be displayed
        tableHeight = m.GetScreenResolution(args.Player)[1] - tableMarginTop - tableMarginBottom;
        float availableEntrySpace = tableHeight - row1Height - row2Height - row3Height - row4Height - (row5Height + tableLineWidth);

        int entriesPerPage = (int)(availableEntrySpace / listEntryHeight);
        pageCount = (int)Math.Ceiling((float)(m.AllPlayers().Length / entriesPerPage));
        if (page > pageCount)
        {
            page = 0;
        }

        // 1 - heading: Servername
        widgets.Add(Widget.MakeSolid(tableX, tableY, tableWidth, row1Height, Color.DarkGreen.ToArgb()));
        widgets.Add(Widget.MakeText(row1, HeadingFont, tableX + Xcenter(tableWidth, TextWidth(row1, HeadingFont)), tableY + tablePadding, TEXT_COLOR.ToArgb()));
        heightOffset += row1Height;

        // 2 - MOTD
        widgets.Add(Widget.MakeSolid(tableX, tableY + heightOffset, tableWidth, row2Height, Color.ForestGreen.ToArgb()));
        widgets.Add(Widget.MakeText(row2, SmallFontBold, tableX + Xcenter(tableWidth, TextWidth(row2, SmallFontBold)), tableY + heightOffset + tablePadding, TEXT_COLOR.ToArgb()));
        heightOffset += row2Height;

        // 3 - server info: IP Motd Serverping
        widgets.Add(Widget.MakeSolid(tableX, tableY + heightOffset, tableWidth, row3Height, Color.DarkSeaGreen.ToArgb()));
        // row3_1 - IP align left
        widgets.Add(Widget.MakeText(row3_1, SmallFont, tableX + tablePadding, tableY + heightOffset + tablePadding, TEXT_COLOR.ToArgb()));
        // row3_2 - Serverping align right
        widgets.Add(Widget.MakeText(row3_2, SmallFont, tableX + tableWidth - TextWidth(row3_2, SmallFont) - tablePadding, tableY + heightOffset + tablePadding, TEXT_COLOR.ToArgb()));
        heightOffset += row3Height;

        // 4 - infoline: Playercount, Page
        widgets.Add(Widget.MakeSolid(tableX, tableY + heightOffset, tableWidth, row4Height, Color.DimGray.ToArgb()));
        // row4_1 PlayerCount
        widgets.Add(Widget.MakeText(row4_1, SmallFont, tableX + tablePadding, tableY + heightOffset + tablePadding, TEXT_COLOR.ToArgb()));
        // row4_2 PlayerCount
        widgets.Add(Widget.MakeText(row4_2, SmallFont, tableX + tableWidth - TextWidth(row4_2, SmallFont) - tablePadding, tableY + heightOffset + tablePadding, TEXT_COLOR.ToArgb()));
        heightOffset += row4Height;

        // 5 - playerlist heading: ID | Player | Ping
        widgets.Add(Widget.MakeSolid(tableX, tableY + heightOffset, tableIdColumnWidth, row5Height, Color.DarkGray.ToArgb()));
        widgets.Add(Widget.MakeSolid(tableX + tableIdColumnWidth, tableY + heightOffset, tablePlayerColumnWidth, row5Height, Color.DarkGray.ToArgb()));
        widgets.Add(Widget.MakeSolid(tableX + tableIdColumnWidth + tablePlayerColumnWidth, tableY + heightOffset, tablePingColumnWidth, row5Height, Color.DarkGray.ToArgb()));
        // separation lines
        widgets.Add(Widget.MakeSolid(tableX + tableIdColumnWidth, tableY + heightOffset, tableLineWidth, row5Height, Color.DimGray.ToArgb()));
        widgets.Add(Widget.MakeSolid(tableX + tableIdColumnWidth + tablePlayerColumnWidth - tableLineWidth, tableY + heightOffset, tableLineWidth, row5Height, Color.DimGray.ToArgb()));
        // row4_1 ID - align center
        widgets.Add(Widget.MakeText(row5_1, NormalFontBold, tableX + Xcenter(tableIdColumnWidth, TextWidth(row5_1, NormalFontBold)), tableY + heightOffset + tablePadding, TEXT_COLOR.ToArgb()));
        // row4_2 Player - align center
        widgets.Add(Widget.MakeText(row5_2, NormalFontBold, tableX + tableIdColumnWidth + (tablePlayerColumnWidth / 2) - (TextWidth(row5_2, NormalFontBold) / 2), tableY + heightOffset + tablePadding, TEXT_COLOR.ToArgb()));
        // row4_3 Ping - align center
        widgets.Add(Widget.MakeText(row5_3, NormalFontBold, tableX + tableIdColumnWidth + tablePlayerColumnWidth + (tablePingColumnWidth / 2) - (TextWidth(row5_3, NormalFontBold) / 2), tableY + heightOffset + tablePadding, TEXT_COLOR.ToArgb()));
        heightOffset += row5Height;
        // horizontal line
        widgets.Add(Widget.MakeSolid(tableX, tableY + heightOffset, tableWidth, tableLineWidth, Color.DimGray.ToArgb()));
        heightOffset += tableLineWidth;

        // 6 - actual playerlist
        // entries:
        Color entryRowColor;
        int[] AllPlayers = m.AllPlayers();
        for (int i = page * entriesPerPage; i < Math.Min(AllPlayers.Length, (page * entriesPerPage) + entriesPerPage); i++)
        {
            if (i % 2 == 0)
            {
                entryRowColor = Color.Gainsboro;
            }
            else
            {
                entryRowColor = Color.Honeydew;
            }

            widgets.Add(Widget.MakeSolid(tableX, tableY + heightOffset, tableIdColumnWidth, listEntryHeight, entryRowColor.ToArgb()));
            widgets.Add(Widget.MakeSolid(tableX + tableIdColumnWidth, tableY + heightOffset, tablePlayerColumnWidth, listEntryHeight, entryRowColor.ToArgb()));
            widgets.Add(Widget.MakeSolid(tableX + tableIdColumnWidth + tablePlayerColumnWidth, tableY + heightOffset, tablePingColumnWidth, listEntryHeight, entryRowColor.ToArgb()));

            // separation lines
            widgets.Add(Widget.MakeSolid(tableX + tableIdColumnWidth, tableY + heightOffset, tableLineWidth, listEntryHeight, Color.DimGray.ToArgb()));
            widgets.Add(Widget.MakeSolid(tableX + tableIdColumnWidth + tablePlayerColumnWidth - tableLineWidth, tableY + heightOffset, tableLineWidth, listEntryHeight, Color.DimGray.ToArgb()));

            widgets.Add(Widget.MakeText(AllPlayers[i].ToString(), NormalFont, tableX + tableIdColumnWidth - TextWidth(AllPlayers[i].ToString(), NormalFont) - tablePadding, tableY + heightOffset + listEntryPaddingTopBottom, TEXT_COLOR.ToArgb()));
            widgets.Add(Widget.MakeText(GetPrefix(AllPlayers[i]) + m.GetPlayerName(AllPlayers[i]), NormalFont, tableX + tableIdColumnWidth + tablePadding, tableY + heightOffset + listEntryPaddingTopBottom, TEXT_COLOR.ToArgb()));
            string pingString;
            if (m.IsBot(AllPlayers[i]))
            {
                pingString = "BOT";
            }
            else
            {
                pingString = ((int)(m.GetPlayerPing(AllPlayers[i]) * 1000)).ToString();
            }

            widgets.Add(Widget.MakeText(pingString, NormalFont, tableX + tableIdColumnWidth + tablePlayerColumnWidth + tablePingColumnWidth - TextWidth(pingString, NormalFont) - tablePadding, tableY + heightOffset + listEntryPaddingTopBottom, TEXT_COLOR.ToArgb()));
            heightOffset += listEntryHeight;
        }

        Widget wtab = Widget.MakeSolid(0, 0, 0, 0, 0);
        wtab.ClickKey = '\t';
        wtab.Id = "Tab";
        widgets.Add(wtab);
        Widget wesc = Widget.MakeSolid(0, 0, 0, 0, 0);
        wesc.ClickKey = (char)27;
        wesc.Id = "Esc";
        widgets.Add(wesc);

        d.Width = m.GetScreenResolution(args.Player)[0];
        d.Height = m.GetScreenResolution(args.Player)[1];
        d.Widgets = widgets.ToArray();
        m.SendDialog(args.Player, "PlayerList", d);
    }

    private int pageCount = 0; //number of pages for player table entries
    private int page = 0; // current displayed page

    // fonts
    public readonly Color TEXT_COLOR = Color.Black;
    public DialogFont HeadingFont = new("Verdana", 11f, DialogFontStyle.Bold);
    public DialogFont NormalFont = new("Verdana", 10f, DialogFontStyle.Regular);
    public DialogFont NormalFontBold = new("Verdana", 10f, DialogFontStyle.Bold);
    public DialogFont SmallFont = new("Verdana", 8f, DialogFontStyle.Regular);
    public DialogFont SmallFontBold = new("Verdana", 8f, DialogFontStyle.Bold);

    private readonly float tableMarginTop = 10;
    private readonly float tableMarginBottom = 10;
    private readonly float tableWidth = 500;
    private float tableHeight = 500;
    private readonly float tablePadding = 5;
    private readonly float listEntryPaddingTopBottom = 2;
    private readonly float tableIdColumnWidth = 50;
    private readonly float tablePlayerColumnWidth = 400;
    private readonly float tablePingColumnWidth = 50;
    private readonly float tableLineWidth = 2;

    public bool NextPage()
    {
        if (page < pageCount)
        {
            page++;
            return true;
        }

        return false;
    }

    public bool PreviousPage()
    {
        if (page > 0)
        {
            page--;
            return true;
        }

        return false;
    }

    private static float Xcenter(float outerWidth, float innerWidth) => (outerWidth / 2) - (innerWidth / 2);

    private static float Ycenter(float outerHeight, float innerHeight) => (outerHeight / 2) - (innerHeight / 2);

    private float TextWidth(string text, DialogFont font) => m.MeasureTextSize(text, font)[0];

    private float TextHeight(string text, DialogFont font) => m.MeasureTextSize(text, font)[1];

    private string CutText(string text, DialogFont font, float maxWidth)
    {
        while (TextWidth(text, font) > maxWidth && text.Length > 3)
        {
            text = text[..^1];
        }

        return text;
    }

    private void OnTabResponse(DialogClickArgs clickArgs)
    {
        if (clickArgs.WidgetId is "Tab" or "Esc")
        {
            m.SendDialog(clickArgs.Player, "PlayerList", null);
            tabOpen.Remove(m.GetPlayerName(clickArgs.Player));
        }
    }

    private readonly Dictionary<string, bool> tabOpen = [];

    private void UpdateTab()
    {
        foreach (KeyValuePair<string, bool> k in new Dictionary<string, bool>(tabOpen))
        {
            foreach (int p in m.AllPlayers())
            {
                if (k.Key == m.GetPlayerName(p))
                {
                    OnTabKey(new SpecialKeyArgs { Player = p, Key = SpecialKey.TabPlayerList });
                    goto nexttab;
                }
            }
            //player disconnected
            tabOpen.Remove(k.Key);
        nexttab:
            ;
        }
    }
}
