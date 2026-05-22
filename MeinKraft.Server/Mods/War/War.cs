using System;
using System.Collections.Generic;
using System.Drawing;

namespace MeinKraft.Mods.War;

public class War : IMod
{
    private readonly TimeSpan RespawnTime = TimeSpan.FromSeconds(30);
    private readonly Dictionary<int, Player> players = [];
    private readonly string BlueColor = "&1";
    private readonly string GreenColor = "&2";
    private readonly string SpectatorColor = "&7";

    private bool spawnedBot = false;
    private DateTime CurrentRespawnTime;

    public bool EnableTeamkill = true;

    public void PreStart(IServerModManager m) => m.RequireMod("CoreBlocks");
    public void Start(IServerModManager manager, IModEvents modEvents)
    {
        m = manager;
        CurrentRespawnTime = DateTime.UtcNow;
        //Basic settings
        m.SetCreative(false);
        m.SetWorldSize(256, 256, 128);
        m.SetWorldDatabaseReadOnly(true); // WarMode.TeamDeathmatch
        m.DisablePrivilege("tp");

        //Register specific functions
        modEvents.PlayerJoin += PlayerJoin;
        modEvents.DialogClick += DialogClickSelectTeam;
        modEvents.DialogClick += DialogClickSelectClass;
        modEvents.DialogClick += DialogClickSelectSubclass;
        modEvents.WeaponHit += Hit;
        modEvents.SpecialKey += RespawnKey;
        modEvents.SpecialKey += OnTabKey;
        modEvents.DialogClick += OnTabResponse;
        modEvents.SpecialKey += OnSelectTeamKey;
        modEvents.ChangedActiveMaterialSlot += UpdatePlayerModel;
        modEvents.WeaponShot += Shot;
        modEvents.PlayerChat += OnChat;
        modEvents.Command += OnCommand;
        modEvents.BlockBuild += OnBuild;
        modEvents.PlayerDeath += OnPlayerDeath;
        //Register timers
        m.RegisterTimer(UpdateMedicalKitAmmoPack, 0.1);
        m.RegisterTimer(UpdateRespawnTimer, 1);
        m.RegisterTimer(UpdateTab, 1);
    }

    public enum WarMode
    {
        Edit,
        TeamDeathmatch,
    }

    private WarMode warmode = WarMode.TeamDeathmatch;

    public enum PlayerClass
    {
        Soldier,
        Medic,
        Support,
    }

    public enum SoldierSubclass
    {
        SubmachineGun,
        Shotgun,
        Rifle,
    }

    public class Player
    {
        public Team team = Team.Spectator;
        public int kills;
        public bool isdead;
        public int following = -1;
        public bool firstteam = true;
        public PlayerClass playerclass;
        public SoldierSubclass soldierSubclass;
        public Dictionary<int, int> totalAmmo = [];
    }

    private IServerModManager m;

    private void PlayerJoin(PlayerJoinArgs args)
    {
        m.SetPlayerHealth(args.Player, 100, 100);
        players[args.Player] = new Player();
        switch (warmode)
        {
            case WarMode.Edit:
                m.EnableExtraPrivilegeToAll("build", false);
                m.EnableFreemove(args.Player, true);
                int posx = m.GetMapSizeX() / 2;
                int posy = m.GetMapSizeY() / 2;
                int posz = BlockHeight(posx, posy);
                m.SetPlayerPosition(args.Player, posx, posy, posz);
                ClearInventory(args.Player);
                GiveAllBlocks(args.Player);
                m.SetGlobalDataNotSaved("enablewater", false);
                break;
            case WarMode.TeamDeathmatch:
                m.SetCreative(false);
                m.EnableExtraPrivilegeToAll("build", true);
                m.EnableFreemove(args.Player, false);
                ShowTeamSelectionDialog(args.Player);
                m.SetGlobalDataNotSaved("enablewater", true);
                break;
        }
    }

    private void GiveAllBlocks(int playerid)
    {
        for (int i = 1; i < m.GetMaxBlockTypes(); i++)
        {
            BlockType b = m.GetBlockType(i);
            if (b != null)
            {
                m.GrabBlocks(playerid, i, 9999);
            }
        }

        m.NotifyInventory(playerid);
    }

    private void ClearInventory(int playerid)
    {
        Inventory inv = m.GetInventory(playerid);
        inv.Boots = null;
        inv.DragDropItem = null;
        inv.Gauntlet = null;
        inv.Helmet = null;
        inv.Items.Clear();
        inv.MainArmor = null;
        Array.Clear(inv.RightHand, 0, inv.RightHand.Length);
        m.NotifyInventory(playerid);
    }

    private void ShowTeamSelectionDialog(int playerid)
    {
        Dialog d = new();
        List<Widget> widgets = [];
        Widget background = new()
        {
            X = 0,
            Y = 0,
            Width = 800,
            Height = 800,
            Image = "SelectTeam"
        };
        widgets.Add(background);
        Widget w1 = new()
        {
            Id = "Team1",
            Text = "Press 1 to join Blue",
            X = 50,
            Y = 400,
            ClickKey = '1'
        };
        widgets.Add(w1);
        Widget w2 = new()
        {
            Text = "Press 2 to join Green",
            Id = "Team2",
            X = 600,
            Y = 400,
            ClickKey = '2'
        };
        widgets.Add(w2);
        Widget w3 = new()
        {
            Text = "Press 3 to spectate",
            Id = "Team3",
            X = 300,
            Y = 400,
            ClickKey = '3'
        };
        widgets.Add(w3);
        d.Width = 800;
        d.Height = 600;
        d.Widgets = [.. widgets];
        m.SendDialog(playerid, "SelectTeam" + playerid, d);
    }

    private void ShowClassSelectionDialog(int playerid)
    {
        Dialog d = new();
        List<Widget> widgets = [];
        Widget background = new()
        {
            X = 0,
            Y = 0,
            Width = 800,
            Height = 800,
            Image = "SelectClass"
        };
        widgets.Add(background);
        string[] classes = ["Soldier", "Medic", "Support"];
        for (int i = 0; i < 3; i++)
        {
            Widget w = new()
            {
                Id = $"Class{i + 1}",
                Text = string.Format("Press {0} for {1}", i + 1, classes[i]),
                X = 50 + (250 * i),
                Y = 400,
                ClickKey = (i + 1).ToString()[0]
            };
            widgets.Add(w);
        }

        d.Width = 800;
        d.Height = 600;
        d.Widgets = [.. widgets];
        m.SendDialog(playerid, "SelectClass" + playerid, d);
    }

    private void ShowSubclassSelectionDialog(int playerid)
    {
        Dialog d = new();
        List<Widget> widgets = [];
        Widget background = new()
        {
            X = 0,
            Y = 0,
            Width = 800,
            Height = 800,
            Image = "SelectSubclass"
        };
        widgets.Add(background);
        string[] subclasses = null;
        if (players[playerid].playerclass == PlayerClass.Soldier)
        {
            subclasses = ["Submachine gun", "Shotgun", "Rifle"];
        }

        if (players[playerid].playerclass == PlayerClass.Medic)
        {
            subclasses = ["Pistol"];
        }

        if (players[playerid].playerclass == PlayerClass.Support)
        {
            subclasses = ["Pistol"];
        }

        for (int i = 0; i < subclasses.Length; i++)
        {
            Widget w = new()
            {
                Id = $"Subclass{i + 1}",
                Text = string.Format("Press {0} for {1}", i + 1, subclasses[i]),
                X = 50 + (275 * i),
                Y = 400,
                ClickKey = (i + 1).ToString()[0]
            };
            widgets.Add(w);
        }

        d.Width = 800;
        d.Height = 600;
        d.Widgets = [.. widgets];
        m.SendDialog(playerid, "SelectSubclass" + playerid, d);
    }

    public enum Team
    {
        Blue,
        Green,
        Spectator,
    }

    private string GetTeamColorString(Team team)
    {
        return team switch
        {
            Team.Blue => BlueColor,
            Team.Green => GreenColor,
            Team.Spectator => SpectatorColor,
            _ => throw new Exception(),
        };
    }

    private void DialogClickSelectTeam(DialogClickArgs args)
    {
        int playerid = args.Player;
        string widget = args.WidgetId;

        if (widget == "Team1")
        {
            m.SendDialog(playerid, "SelectTeam" + playerid, null);
            //if (players[playerid].team == Team.Blue && (!players[playerid].firstteam))
            {
                //return;
            }

            if (players[playerid].team != Team.Blue)
            {
                //Player changed team
                players[playerid].team = Team.Blue;
                players[playerid].kills = 0;
                m.SendMessageToAll(string.Format("{0} joins {1}&f team.", m.GetPlayerName(playerid), $"{BlueColor} Blue"));
            }

            m.SetPlayerSpectator(playerid, false);
            UpdatePlayerModel(new ChangedActiveMaterialSlotArgs { Player = playerid });
            m.EnableFreemove(playerid, false);
            ShowClassSelectionDialog(playerid);
        }

        if (widget == "Team2")
        {
            m.SendDialog(playerid, "SelectTeam" + playerid, null);
            //if (players[playerid].team == Team.Green && (!players[playerid].firstteam))
            {
                //return;
            }

            if (players[playerid].team != Team.Green)
            {
                //Player changed team
                players[playerid].team = Team.Green;
                players[playerid].kills = 0;
                m.SendMessageToAll(string.Format("{0} joins {1}&f team.", m.GetPlayerName(playerid), GreenColor + " " + "Green"));
            }

            m.SetPlayerSpectator(playerid, false);
            UpdatePlayerModel(new ChangedActiveMaterialSlotArgs { Player = playerid });
            m.EnableFreemove(playerid, false);
            ShowClassSelectionDialog(playerid);
        }

        if (widget == "Team3")
        {
            m.SendDialog(playerid, "SelectTeam" + playerid, null);
            if (players[playerid].team == Team.Spectator && (!players[playerid].firstteam))
            {
                return;
            }

            players[playerid].team = Team.Spectator;
            players[playerid].kills = 0;
            m.SetPlayerSpectator(playerid, true);
            UpdatePlayerModel(new ChangedActiveMaterialSlotArgs { Player = playerid });
            m.EnableFreemove(playerid, true);
            m.SendMessageToAll(string.Format("{0} becomes a &7 spectator&f.", m.GetPlayerName(playerid)));
            ClearInventory(playerid);
        }

        if (widget is "Team1" or "Team2" or "Team3")
        {
            if (!spawnedBot)
            {
                spawnedBot = true;
                //if (System.Diagnostics.Debugger.IsAttached)
                //{
                //    int bot = m.AddBot("bot");
                //    PlayerJoin(bot);
                //    DialogClickSelectTeam(bot, "Team2");
                //    Respawn(bot);
                //}
            }
        }
    }

    private void DialogClickSelectClass(DialogClickArgs args)
    {
        if (args.WidgetId == "Class1")
        {
            players[args.Player].playerclass = PlayerClass.Soldier;
            ShowSubclassSelectionDialog(args.Player);
        }

        if (args.WidgetId == "Class2")
        {
            players[args.Player].playerclass = PlayerClass.Medic;
            ShowSubclassSelectionDialog(args.Player);
        }

        if (args.WidgetId == "Class3")
        {
            players[args.Player].playerclass = PlayerClass.Support;
            ShowSubclassSelectionDialog(args.Player);
        }

        if (args.WidgetId is "Class1" or "Class2" or "Class3")
        {
            m.SendDialog(args.Player, "SelectClass" + args.Player, null);
        }
    }

    private void DialogClickSelectSubclass(DialogClickArgs args)
    {
        if (args.WidgetId is not ("Subclass1" or "Subclass2" or "Subclass3"))
            return;

        if (players[args.Player].firstteam)
            Respawn(args.Player);
        else
            Die(args.Player);

        players[args.Player].firstteam = false;

        m.SendDialog(args.Player, "SelectSubclass" + args.Player, null);

        PlayerClass pclass = players[args.Player].playerclass;
        if (pclass == PlayerClass.Soldier)
        {
            if (args.WidgetId == "Subclass1")
                players[args.Player].soldierSubclass = SoldierSubclass.SubmachineGun;

            if (args.WidgetId == "Subclass2")
                players[args.Player].soldierSubclass = SoldierSubclass.Shotgun;

            if (args.WidgetId == "Subclass3")
                players[args.Player].soldierSubclass = SoldierSubclass.Rifle;
        }

        if (pclass == PlayerClass.Medic)
        {
            if (args.WidgetId == "Subclass1")
            {
                //todo medic subclass
            }
        }

        if (pclass == PlayerClass.Support)
        {
            if (args.WidgetId == "Subclass1")
            {
                //todo support subclass
            }
        }

        ResetInventoryOnRespawn(args.Player);
    }

    private void ResetInventoryOnRespawn(int playerid)
    {
        ClearInventory(playerid);
        if (players[playerid].team == Team.Spectator)
        {
            //Don't give spectators weapons when they die.
            return;
        }

        PlayerClass pclass = players[playerid].playerclass;
        if (pclass == PlayerClass.Soldier)
        {
            SoldierSubclass sclass = players[playerid].soldierSubclass;
            if (sclass == SoldierSubclass.SubmachineGun)
            {
                m.GrabBlock(playerid, m.GetBlockId("SubmachineGun"));
                m.GrabBlock(playerid, m.GetBlockId("Pistol"));
                m.GrabBlock(playerid, m.GetBlockId("Grenade"));
            }

            if (sclass == SoldierSubclass.Shotgun)
            {
                m.GrabBlock(playerid, m.GetBlockId("Shotgun"));
                m.GrabBlock(playerid, m.GetBlockId("Pistol"));
                m.GrabBlock(playerid, m.GetBlockId("Grenade"));
            }

            if (sclass == SoldierSubclass.Rifle)
            {
                m.GrabBlock(playerid, m.GetBlockId("Rifle"));
                m.GrabBlock(playerid, m.GetBlockId("Pistol"));
                m.GrabBlock(playerid, m.GetBlockId("Grenade"));
            }
        }

        if (pclass == PlayerClass.Medic)
        {
            m.GrabBlock(playerid, m.GetBlockId("Pistol"));
            for (int i = 0; i < 4; i++)
            {
                m.GrabBlock(playerid, m.GetBlockId("MedicalKit"));
            }
        }

        if (pclass == PlayerClass.Support)
        {
            m.GrabBlock(playerid, m.GetBlockId("Pistol"));
            for (int i = 0; i < 5; i++)
            {
                m.GrabBlock(playerid, m.GetBlockId("AmmoPack"));
            }
        }

        m.NotifyInventory(playerid);
        Inventory inv = m.GetInventory(playerid);
        for (int i = 0; i < 10; i++)
        {
            InventoryItem item = inv.RightHand[i];
            if (item != null && item.InventoryItemType == InventoryItemType.Block)
            {
                BlockType block = m.GetBlockType(item.BlockId);
                if (block.IsPistol)
                {
                    players[playerid].totalAmmo[item.BlockId] = block.AmmoTotal;
                }
            }
        }

        m.NotifyAmmo(playerid, players[playerid].totalAmmo);
    }

    private void OnSelectTeamKey(SpecialKeyArgs args)
    {
        if (args.Key != SpecialKey.SelectTeam)
            return;

        if (warmode == WarMode.Edit)
            return;

        ShowTeamSelectionDialog(args.Player);
    }

    private void OnPlayerDeath(PlayerDeathArgs args)
    {
        string deathMessage = "";
        switch (args.Reason)
        {
            case DeathReason.FallDamage:
                Die(args.Player);
                deathMessage = string.Format("{0}{1} &7was doomed to fall.", GetTeamColorString(players[args.Player].team), m.GetPlayerName(args.Player));
                break;
            case DeathReason.BlockDamage:
                if (args.SourceId == m.GetBlockId("Lava"))
                {
                    Die(args.Player);
                    deathMessage = string.Format("{0}{1} &7thought they could swim in Lava.", GetTeamColorString(players[args.Player].team), m.GetPlayerName(args.Player));
                }
                else if (args.SourceId == m.GetBlockId("Fire"))
                {
                    Die(args.Player);
                    deathMessage = string.Format("{0}{1} &7was burned alive.", GetTeamColorString(players[args.Player].team), m.GetPlayerName(args.Player));
                }
                else
                {
                    Die(args.Player);
                    deathMessage = string.Format("{0}{1} &7was killed by {2}.", GetTeamColorString(players[args.Player].team), m.GetPlayerName(args.Player), m.GetBlockName(args.SourceId));
                }

                break;
            case DeathReason.Drowning:
                Die(args.Player);
                deathMessage = string.Format("{0}{1} &7tried to breathe under water.", GetTeamColorString(players[args.Player].team), m.GetPlayerName(args.Player));
                break;
            case DeathReason.Explosion:
                if (!EnableTeamkill)
                {
                    if (players[args.SourceId].team == players[args.Player].team)
                    {
                        break;
                    }
                }
                //Check if one of the players is spectator
                if (players[args.SourceId].team == Team.Spectator || players[args.Player].team == Team.Spectator)
                {
                    //Just here for safety. Spectators shouldn't have weapons...
                    break;
                }
                //Check if one of the players is dead
                if (players[args.Player].isdead || players[args.SourceId].isdead)
                {
                    break;
                }

                Die(args.Player);
                if (args.SourceId == args.Player)
                {
                    deathMessage = string.Format("{0}{1} &7blew himself up.", GetTeamColorString(players[args.Player].team), m.GetPlayerName(args.Player));
                    break;
                }

                if (players[args.SourceId].team != players[args.Player].team)
                {
                    players[args.SourceId].kills = players[args.SourceId].kills + 1;
                }
                else
                {
                    players[args.SourceId].kills = players[args.SourceId].kills - 2;
                }

                if (players[args.SourceId].team == players[args.Player].team)
                {
                    deathMessage = string.Format("{0}{1} &7was blown into pieces by {2}{3}. - {4}TEAMKILL", GetTeamColorString(players[args.Player].team), m.GetPlayerName(args.Player), GetTeamColorString(players[args.SourceId].team), m.GetPlayerName(args.SourceId), GameConstants.colorError);
                }
                else
                {
                    deathMessage = string.Format("{0}{1} &7was blown into pieces by {2}{3}&7.", GetTeamColorString(players[args.Player].team), m.GetPlayerName(args.Player), GetTeamColorString(players[args.SourceId].team), m.GetPlayerName(args.SourceId));
                }

                break;
            default:
                Die(args.Player);
                deathMessage = string.Format("{0}{1} &7died.", GetTeamColorString(players[args.Player].team), m.GetPlayerName(args.Player));
                break;
        }

        if (!string.IsNullOrEmpty(deathMessage))
        {
            m.SendMessageToAll(deathMessage);
        }
    }

    private void Respawn(int playerid)
    {
        int posx = -1;
        int posy = -1;
        int posz = -1;
        switch (players[playerid].team)
        {
            case Team.Blue:
                posx = m.GetMapSizeX() / 2;
                posy = 50;
                break;
            case Team.Green:
                posx = m.GetMapSizeX() / 2;
                posy = m.GetMapSizeY() - 50;
                break;
            case Team.Spectator:
                posx = m.GetMapSizeX() / 2;
                posy = m.GetMapSizeY() / 2;
                break;
        }

        posz = BlockHeight(posx, posy);
        m.SetPlayerPosition(playerid, posx, posy, posz);
        ResetInventoryOnRespawn(playerid);
    }

    public int BlockHeight(int x, int y)
    {
        for (int z = m.GetMapSizeZ() - 1; z >= 0; z--)
        {
            if (m.GetBlock(x, y, z) != 0)
            {
                return z + 1;
            }
        }

        return m.GetMapSizeZ() / 2;
    }

    private void Shot(WeaponShotArgs args)
    {
        if (!players[args.SourcePlayer].totalAmmo.TryGetValue(args.Block, out int value))
        {
            value = 0;
            players[args.SourcePlayer].totalAmmo[args.Block] = value;
        }

        players[args.SourcePlayer].totalAmmo[args.Block] = value - 1;
        m.NotifyAmmo(args.SourcePlayer, players[args.SourcePlayer].totalAmmo);
    }

    private void Hit(WeaponHitArgs args)
    {
        if (!EnableTeamkill)
        {
            if (players[args.SourcePlayer].team == players[args.TargetPlayer].team)
                return;
        }

        //Check if one of the players is a spectator
        if (players[args.SourcePlayer].team == Team.Spectator || players[args.TargetPlayer].team == Team.Spectator)
            return;

        //Check if one of the players is dead
        if (players[args.TargetPlayer].isdead || players[args.SourcePlayer].isdead)
            return;

        {
            float x1 = m.GetPlayerPositionX(args.SourcePlayer);
            float y1 = m.GetPlayerPositionY(args.SourcePlayer);
            float z1 = m.GetPlayerPositionZ(args.SourcePlayer);
            float x2 = m.GetPlayerPositionX(args.TargetPlayer);
            float y2 = m.GetPlayerPositionY(args.TargetPlayer);
            float z2 = m.GetPlayerPositionZ(args.TargetPlayer);
            float dx = x1 - x2;
            float dy = y1 - y2;
            float dz = z1 - z2;
            float dist = MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            dx = dx / dist * 0.1f;
            dy = dy / dist * 0.1f;
            dz = dz / dist * 0.1f;
            m.SendExplosion(args.TargetPlayer, dx, dy, dz, true, m.GetBlockType(args.Block).ExplosionRange, m.GetBlockType(args.Block).ExplosionTime);
        }

        int health = m.GetPlayerHealth(args.TargetPlayer);
        int dmghead = 50;
        int dmgbody = 15;
        if (m.GetBlockType(args.Block).DamageHead != 0)
            dmghead = (int)m.GetBlockType(args.Block).DamageHead;

        if (m.GetBlockType(args.Block).DamageBody != 0)
            dmgbody = (int)m.GetBlockType(args.Block).DamageBody;

        health -= args.Headshot ? dmghead : dmgbody;
        if (health <= 0)
        {
            if (players[args.SourcePlayer].team != players[args.TargetPlayer].team)
                players[args.SourcePlayer].kills = players[args.SourcePlayer].kills + 1;
            else
                players[args.SourcePlayer].kills = players[args.SourcePlayer].kills - 2;

            Die(args.TargetPlayer);
            if (players[args.SourcePlayer].team == players[args.TargetPlayer].team)
                m.SendMessageToAll(string.Format("{0} kills {1} - " + GameConstants.colorError + "TEAMKILL", m.GetPlayerName(args.SourcePlayer), m.GetPlayerName(args.TargetPlayer)));
            else
                m.SendMessageToAll(string.Format("{0} kills {1}", m.GetPlayerName(args.SourcePlayer), m.GetPlayerName(args.TargetPlayer)));
        }
        else
        {
            m.SetPlayerHealth(args.TargetPlayer, health, m.GetPlayerMaxHealth(args.TargetPlayer));
            m.PlaySoundAt((int)m.GetPlayerPositionX(args.TargetPlayer),
                          (int)m.GetPlayerPositionY(args.TargetPlayer),
                          (int)m.GetPlayerPositionZ(args.TargetPlayer), "grunt1.ogg");
        }
    }

    private void Die(int player)
    {
        m.PlaySoundAt((int)m.GetPlayerPositionX(player),
                      (int)m.GetPlayerPositionY(player),
                      (int)m.GetPlayerPositionZ(player), "death.ogg");
        //Respawn(targetplayer);
        players[player].isdead = true;
        m.SetPlayerHealth(player, m.GetPlayerMaxHealth(player), m.GetPlayerMaxHealth(player));
        m.FollowPlayer(player, player, true);
        UpdatePlayerModel(new ChangedActiveMaterialSlotArgs { Player = player });
    }

    private void RespawnKey(SpecialKeyArgs args)
    {
        if (args.Key != SpecialKey.Respawn)
            return;

        if (warmode == WarMode.Edit)
            return;

        if (players[args.Player].isdead)
            return;     //Don't allow dead players to respawn

        m.SendMessage(args.Player, "Respawn.");
        Die(args.Player);
    }

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
        float tableX = XCenter(m.GetScreenResolution(args.Player)[0], tableWidth);
        float tableY = tableMarginTop;

        // text to draw
        string row1 = m.ServerName;
        row1 = CutText(row1, HeadingFont, tableWidth - (2 * tablePadding));

        string row2 = m.ServerMotd;
        row2 = CutText(row2, SmallFontBold, tableWidth - (2 * tablePadding));

        string row3_1 = $"IP: {m.ServerIp}:{m.ServerPort}";
        string row3_2 = $"{(int)(m.GetPlayerPing(args.Player) * 1000)}ms";

        string row4_1 = $"Players: {m.AllPlayers().Length}";
        string row4_2 = $"Page: {page + 1}/{pageCount + 1}";

        string row5_1 = "ID";
        //string row5_2 = "Player";
        //string row5_3 = "Ping";

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
        widgets.Add(Widget.MakeText(row1, HeadingFont, tableX + XCenter(tableWidth, TextWidth(row1, HeadingFont)), tableY + tablePadding, TEXT_COLOR.ToArgb()));
        heightOffset += row1Height;

        // 2 - MOTD
        widgets.Add(Widget.MakeSolid(tableX, tableY + heightOffset, tableWidth, row2Height, Color.ForestGreen.ToArgb()));
        widgets.Add(Widget.MakeText(row2, SmallFontBold, tableX + XCenter(tableWidth, TextWidth(row2, SmallFontBold)), tableY + heightOffset + tablePadding, TEXT_COLOR.ToArgb()));
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

        Dictionary<Team, List<int>> playersByTeam = new()
        {
            [Team.Blue] = [],
            [Team.Spectator] = [],
            [Team.Green] = []
        };
        int[] AllPlayers = m.AllPlayers();
        foreach (int p in AllPlayers)
        {
            playersByTeam[players[p].team].Add(p);
        }

        Team[] allteams = [Team.Blue, Team.Spectator, Team.Green];
        for (int t = 0; t < allteams.Length; t++)
        {
            List<int> players = playersByTeam[allteams[t]];
            players.Sort((a, b) => this.players[b].kills.CompareTo(this.players[a].kills));
            for (int i = 0; i < players.Count; i++)
            {
                string s = string.Format("{0} {1}ms {2} kills", m.GetPlayerName(players[i]), (int)(m.GetPlayerPing(players[i]) * 1000), this.players[players[i]].kills);
                widgets.Add(Widget.MakeText(s, NormalFont, tableX + (200 * t), tableY + heightOffset + (listEntryHeight * i), Color.White.ToArgb()));
            }
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
        d.Widgets = [.. widgets];
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
    //private float tableIdColumnWidth = 50;
    //private float tablePlayerColumnWidth = 400;
    //private float tablePingColumnWidth = 50;
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

    private static float XCenter(float outerWidth, float innerWidth) => (outerWidth / 2) - (innerWidth / 2);

    private static float YCenter(float outerHeight, float innerHeight) => (outerHeight / 2) - (innerHeight / 2);

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

    private void OnTabResponse(DialogClickArgs args)
    {
        if (args.WidgetId is "Tab" or "Esc")
        {
            m.SendDialog(args.Player, "PlayerList", null);
            tabOpen.Remove(m.GetPlayerName(args.Player));
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

    private void UpdatePlayerModel(ChangedActiveMaterialSlotArgs args)
    {
        Inventory inv = m.GetInventory(args.Player);
        InventoryItem item = inv.RightHand[m.GetActiveMaterialSlot(args.Player)];
        int blockid = 0;
        if (item != null && item.InventoryItemType == InventoryItemType.Block)
        {
            blockid = item.BlockId;
        }

        string model = "playerwar.txt";
        if (blockid == m.GetBlockId("Pistol"))
        {
            model = "playerwarpistol.txt";
        }

        if (blockid == m.GetBlockId("SubmachineGun"))
        {
            model = "playerwarsubmachinegun.txt";
        }

        if (blockid == m.GetBlockId("Shotgun"))
        {
            model = "playerwarshotgun.txt";
        }

        if (blockid == m.GetBlockId("Rifle"))
        {
            model = "playerwarrifle.txt";
        }

        if (players[args.Player].isdead)
        {
            model = "playerwardead.txt";
        }

        m.SetPlayerHeight(args.Player, 2.2f, 2.4f);
        Team team = players[args.Player].team;
        switch (team)
        {
            case Team.Blue:
                m.SetPlayerModel(args.Player, model, "playerblue.png");
                break;
            case Team.Green:
                m.SetPlayerModel(args.Player, model, "playergreen.png");
                break;
            case Team.Spectator:
                m.SetPlayerModel(args.Player, model, "mineplayer.png");
                break;
        }
    }

    private void UpdateRespawnTimer()
    {
        int[] allplayers = m.AllPlayers();
        int secondsToRespawn = (int)(CurrentRespawnTime + RespawnTime - DateTime.UtcNow).TotalSeconds;
        if (secondsToRespawn <= 0)
        {
            for (int i = 0; i < allplayers.Length; i++)
            {
                int p = allplayers[i];
                if (!players.ContainsKey(p))
                {
                    //Skip this player as he hasn't joined yet
                    continue;
                }

                if (players[p].isdead)
                {
                    m.SendDialog(p, "RespawnCountdown" + p, null);
                    m.FollowPlayer(p, -1, false);
                    Respawn(p);
                    players[p].isdead = false;
                    UpdatePlayerModel(new ChangedActiveMaterialSlotArgs { Player = p });
                }
            }

            CurrentRespawnTime = DateTime.UtcNow;
        }

        for (int i = 0; i < allplayers.Length; i++)
        {
            int p = allplayers[i];
            if (!players.ContainsKey(p))
            {
                //Skip this player as he hasn't joined yet
                continue;
            }

            if (players[p].isdead)
            {
                Dialog d = new()
                {
                    IsModal = false
                };
                string text = secondsToRespawn.ToString();
                DialogFont f = new("Verdana", 60f, DialogFontStyle.Regular);
                Widget w = Widget.MakeText(text, f, -m.MeasureTextSize(text, f)[0] / 2, -200, Color.Red.ToArgb());
                d.Widgets = new Widget[1];
                d.Widgets[0] = w;
                m.SendDialog(p, "RespawnCountdown" + p, d);
            }
        }
    }

    private void UpdateMedicalKitAmmoPack()
    {
        if (warmode == WarMode.Edit)
        {
            return;
        }

        int[] allplayers = m.AllPlayers();
        int medicalkit = m.GetBlockId("MedicalKit");
        int ammopack = m.GetBlockId("AmmoPack");
        foreach (int p in allplayers)
        {
            int px = (int)m.GetPlayerPositionX(p);
            int py = (int)m.GetPlayerPositionY(p);
            int pz = (int)m.GetPlayerPositionZ(p);
            if (m.IsValidPos(px, py, pz))
            {
                int block = m.GetBlock(px, py, pz);
                if (block == medicalkit)
                {
                    int health = m.GetPlayerHealth(p);
                    int maxhealth = m.GetPlayerMaxHealth(p);
                    if (health >= maxhealth)
                    {
                        continue;
                    }

                    health += 30;
                    if (health > maxhealth)
                    {
                        health = maxhealth;
                    }

                    m.SetPlayerHealth(p, health, maxhealth);
                    m.SetBlock(px, py, pz, 0);
                    //m.PlaySoundAt((int)m.GetPlayerPositionX(targetplayer),
                    //    (int)m.GetPlayerPositionY(targetplayer),
                    //    (int)m.GetPlayerPositionZ(targetplayer), "heal.ogg");
                }

                if (block == ammopack)
                {
                    foreach (var k in new List<int>(players[p].totalAmmo.Keys))
                    {
                        int ammo = 0;
                        if (players[p].totalAmmo.ContainsKey(k))
                        {
                            ammo = players[p].totalAmmo[k];
                        }

                        ammo += m.GetBlockType(k).AmmoTotal / 3;
                        if (ammo > m.GetBlockType(k).AmmoTotal)
                        {
                            ammo = m.GetBlockType(k).AmmoTotal;
                        }

                        players[p].totalAmmo[k] = ammo;
                    }

                    m.NotifyAmmo(p, players[p].totalAmmo);
                    m.SetBlock(px, py, pz, 0);
                }
            }
        }
    }

    private void OnChat(PlayerChatArgs args)
    {
        if (warmode == WarMode.Edit)
            return;

        int[] allplayers = m.AllPlayers();
        string sender = m.GetPlayerName(args.Player);
        string senderColorString = GetTeamColorString(players[args.Player].team);
        string s = args.Message;
        bool toteam = args.ToTeam;

        if (players[args.Player].team == Team.Spectator)
            toteam = true;

        if (toteam)
            s = GetTeamColorString(players[args.Player].team) + s;

        foreach (int p in allplayers)
        {
            if (toteam)
            {
                if (!(players[p].team == players[args.Player].team || players[p].team == Team.Spectator))
                    continue;
            }

            m.SendMessage(p, $"{senderColorString}{sender}&f: {s}");
        }

        if (players[args.Player].team == Team.Spectator)
        {
            Console.WriteLine($"[Spectator] {sender}: {s}");
        }
        else
        {
            if (toteam)
            {
                if (players[args.Player].team == Team.Blue)
                    Console.WriteLine($"[Blue] {sender}: {s}");
                else
                    Console.WriteLine($"[Green] {sender}: {s}");
            }
            else
            {
                Console.WriteLine($"[Players] {sender}: {s}");
            }
        }

        args.FinalMessage = null;
    }

    private void OnCommand(CommandArgs args)
    {
        if (args.Command == "mode")
        {
            if (!m.PlayerHasPrivilege(args.Player, "mode"))
            {
                m.SendMessage(args.Player, GameConstants.colorError + "No privilege: mode");
                args.Handled = true;
                return;
            }

            if (args.Argument == "edit")
            {
                warmode = WarMode.Edit;
               // m.LoadWorld(m.CurrentWorld);
                m.SetWorldDatabaseReadOnly(false);
                Restart();
            }
            else if (args.Argument == "tdm")
            {
                warmode = WarMode.TeamDeathmatch;
              //  m.LoadWorld(m.CurrentWorld);
                m.SetWorldDatabaseReadOnly(true);
                Restart();
            }
            else
            {
                m.SendMessage(args.Player, GameConstants.colorError + "Usage: /mode [edit/tdm]");
            }

            args.Handled = true;
        }
    }

    private void Restart()
    {
        int[] allplayers = m.AllPlayers();
        foreach (int p in allplayers)
        {
            PlayerJoin(new PlayerJoinArgs { Player = p });
        }
    }

    private void OnBuild(BlockBuildArgs args)
    {
        if (m.GetBlockNameAt(args.X, args.Y, args.Z) == "Water")
        {
            m.SetBlock(args.X, args.Y, args.Z, 0);
        }
    }
}
