public partial class Game
{
    public int ChatLinesCount { get; set; }
    public string GuiTypingBuffer { get; set; }
    public bool IsTyping { get; set; }

    // -------------------------------------------------------------------------
    // Chat line display
    // -------------------------------------------------------------------------

    public void AddChatLine(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return;
        }

        string linkTarget = ExtractLink(s);
        bool containsLink = linkTarget != null;
        int now = gameService.TimeMillisecondsFromStart;

        if (s.Length > ChatLines.Count && ChatLines.Count > 0)
        {
            for (int i = 0; i <= s.Length / ChatLines.Count; i++)
            {
                int displayLength = Math.Min(ChatLines.Count, s.Length - (i * ChatLines.Count));
                string chunk = s.Substring(i * ChatLines.Count, displayLength);
                ChatLinesAdd(containsLink
                    ? Chatline.CreateClickable(chunk, now, linkTarget)
                    : Chatline.Create(chunk, now));
            }
        }
        else
        {
            ChatLinesAdd(containsLink
                ? Chatline.CreateClickable(s, now, linkTarget)
                : Chatline.Create(s, now));
        }
    }

    private static string ExtractLink(string s)
    {
        foreach (string prefix in new[] { "https://", "http://" })
        {
            if (s.Contains(prefix))
            {
                foreach (string word in s.Split(' '))
                {
                    if (word.Contains(prefix, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return word;
                    }
                }
            }
        }

        return null;
    }

    private void ChatLinesAdd(Chatline chatline)
    {
        if (ChatLinesCount >= ChatLinesMax)
        {
            Chatline[] lines2 = new Chatline[ChatLinesMax * 2];
            for (int i = 0; i < ChatLinesMax; i++)
            {
                lines2[i] = ChatLines[i];
            }

            ChatLines = [.. lines2];
            ChatLinesMax *= 2;
        }

        ChatLines.Add(chatline);
    }

    // -------------------------------------------------------------------------
    // Typing state
    // -------------------------------------------------------------------------

    public void StartTyping()
    {
        GuiTyping = TypingState.Typing;
        IsTyping = true;
        GuiTypingBuffer = "";
        IsTeamchat = false;
    }

    public void StopTyping() => GuiTyping = TypingState.None;

    // -------------------------------------------------------------------------
    // Chat / command execution
    // -------------------------------------------------------------------------

    public void ExecuteChat(string s_)
    {
        if (string.IsNullOrEmpty(s_))
        {
            return;
        }

        if (s_.StartsWith("."))
        {
            ExecuteClientCommand(s_);
        }
        else
        {
            // Regular chat message or server command — send to server.
            string chatline = GuiTypingBuffer[..Math.Min(GuiTypingBuffer.Length, 4096)];
            SendChat(chatline);
        }
    }

    public bool BoolCommandArgument(string arguments)
    {
        arguments = arguments.Trim();
        return arguments is "" or "1" or "on" or "yes";
    }

    private void ExecuteClientCommand(string s_)
    {
        string[] ss = s_.Split(' ');
        string cmd = ss[0][1..];
        int spaceIndex = s_.IndexOf(" ", StringComparison.InvariantCultureIgnoreCase);
        string arguments = spaceIndex >= 0 ? s_[spaceIndex..].Trim() : "";
        string strFreemoveNotAllowed = Language.FreemoveNotAllowed();

        switch (cmd)
        {
            case "clients":
                AddChatLine("Clients:");
                for (int i = 0; i < Entities.Count; i++)
                {
                    Entity entity = Entities[i];
                    if (entity == null || entity.DrawName == null || !entity.DrawName.ClientAutoComplete)
                    {
                        continue;
                    }

                    AddChatLine(string.Format("{0} {1}", i.ToString(), entity.DrawName.Name));
                }

                break;

            case "reconnect":
                Reconnect();
                break;

            case "m":
                mouseSmoothing = !mouseSmoothing;
                AddChatLine(mouseSmoothing ? "Mouse smoothing enabled." : "Mouse smoothing disabled.");
                break;

            case "pos":
                EnableDrawPosition = BoolCommandArgument(arguments);
                break;

            case "noclip":
                Controls.NoClip = BoolCommandArgument(arguments);
                break;

            case "freemove":
                if (!AllowFreeMove)
                {
                    AddChatLine(strFreemoveNotAllowed);
                    return;
                }

                Controls.FreeMove = BoolCommandArgument(arguments);
                break;

            case "gui":
                ENABLE_DRAW2D = BoolCommandArgument(arguments);
                break;

            default:
                if (arguments != "")
                {
                    ExecuteClientCommandWithArg(cmd, arguments, strFreemoveNotAllowed);
                }
                else
                {
                    // No matching command — send as chat.
                    string chatline = GuiTypingBuffer[..Math.Min(GuiTypingBuffer.Length, 256)];
                    SendChat(chatline);
                }

                break;
        }

        // Always dispatch to client mods regardless of whether a command matched.
        for (int i = 0; i < ClientMods.Count; i++)
        {
            ClientCommandArgs args = new() { Arguments = arguments, Command = cmd };
            ClientMods[i].OnClientCommand(args);
        }
    }

    private void ExecuteClientCommandWithArg(string cmd, string arguments, string strFreemoveNotAllowed)
    {
        switch (cmd)
        {
            case "fog":
                int foglevel = int.Parse(arguments);
                foglevel = Math.Min(foglevel, 1024);
                if (foglevel % 2 == 0)
                {
                    foglevel--;
                }

                Config3d.ViewDistance = foglevel;
                OnResize();
                break;

            case "fov":
                int arg = int.Parse(arguments);
                int minfov = IsSinglePlayer ? 1 : 60;
                int maxfov = 179;
                if (arg < minfov || arg > maxfov)
                {
                    AddChatLine(string.Format("Valid field of view: {0}-{1}", minfov, maxfov));
                }
                else
                {
                    fov = 2 * MathF.PI * (arg / 360);
                    OnResize();
                }

                break;

            case "movespeed":
                if (!AllowFreeMove)
                {
                    AddChatLine(strFreemoveNotAllowed);
                    return;
                }

                float speed = float.Parse(arguments);
                if (speed > 500)
                {
                    AddChatLine("Entered movespeed to high! max. 500x");
                }
                else
                {
                    MoveSpeed = Basemovespeed * speed;
                    AddChatLine(string.Format("Movespeed: {0}x", arguments));
                }

                break;

            case "serverinfo":
                string[] split = arguments.Split(':');
                if (split.Length == 2)
                {
                    (QueryResult? result, string? message) = Task.Run(() => new QueryClient(gameService.NetworkService).QueryAsync(split[0], int.Parse(split[1]))).GetAwaiter().GetResult();

                    if (result != null)
                    {
                        AddChatLine(result.GameMode);
                        AddChatLine(result.MapSizeX.ToString());
                        AddChatLine(result.MapSizeY.ToString());
                        AddChatLine(result.MapSizeZ.ToString());
                        AddChatLine(result.MaxPlayers.ToString());
                        AddChatLine(result.Motd);
                        AddChatLine(result.Name);
                        AddChatLine(result.PlayerCount.ToString());
                        AddChatLine(result.PlayerList);
                        AddChatLine(result.Port.ToString());
                        AddChatLine(result.PublicHash);
                        AddChatLine(result.ServerVersion);
                    }

                    AddChatLine(message);
                }

                break;
        }
    }
}