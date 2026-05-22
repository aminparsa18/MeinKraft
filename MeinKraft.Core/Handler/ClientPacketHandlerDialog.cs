using MeinKraft;

/// <summary>
/// Handles <see cref="Packet_ServerIdEnum.Dialog"/> packets,
/// opening, updating, or closing server-driven modal and non-modal dialogs.
/// </summary>
public class ClientPacketHandlerDialog : ClientPacketHandler
{
    public ClientPacketHandlerDialog(IGameWindowService gameService, IGame game) : base(gameService, game)
    {
    }

    public override void Handle(Packet_Server packet)
    {
        Packet_ServerDialog d = packet.Dialog;

        // ── Cache the lookup result — previously called up to 4 times per path ─
        int dialogIdx = game.GetDialogId(d.DialogId);

        if (d.Dialog == null)
        {
            // Server is closing this dialog.
            if (dialogIdx != -1 && game.Dialogs[dialogIdx].Value.IsModal)
            {
                game.GuiStateBackToGame();
            }

            if (dialogIdx != -1)
            {
                game.Dialogs[dialogIdx] = null;
            }

            if (game.Dialogs.Count == 0)
            {
                game.SetFreeMouse(false);
            }
        }
        else
        {
            // Server is opening or updating a dialog.
            VisibleDialog d2 = new()
            {
                Key = d.DialogId,
                Value = d.Dialog,
            };
            d2.Screen = ConvertDialog(d2.Value);
            d2.Screen.SetGame(game);

            if (dialogIdx == -1)
            {
                game.Dialogs.Add(game.Dialogs.Count, d2);
            }
            else
            {
                game.Dialogs[dialogIdx] = d2;
            }

            if (d.Dialog.IsModal)
            {
                game.GuiState = GameState.ModalDialog;
                game.SetFreeMouse(true);
            }
        }
    }

    private ModScreen ConvertDialog(Dialog p)
    {
        DialogScreen s = new(gameService, game)
        {
            widgets = new MenuWidget[p.Widgets.Length],
            WidgetCount = p.Widgets.Length,
        };

        for (int i = 0; i < p.Widgets.Length; i++)
        {
            Widget a = p.Widgets[i];
            MenuWidget b = new();

            // ── Single switch instead of four sequential if-blocks ────────────
            b.Type = a.Type switch
            {
                WidgetType.Text => UIWidgetType.Label,
                WidgetType.Image => UIWidgetType.Button,
                WidgetType.TextBox => UIWidgetType.Textbox,
                _ => b.Type,
            };

            b.X = a.X;
            b.Y = a.Y;
            b.Sizex = a.Width;
            b.Sizey = a.Height;
            b.Text = a.Text;

            // ── Single null-check, chained Replace calls ──────────────────────
            if (b.Text != null)
            {
                b.Text = b.Text
                    .Replace("!SERVER_IP!", game.ServerInfo.ConnectData.Ip)
                    .Replace("!SERVER_PORT!", game.ServerInfo.ConnectData.Port.ToString());
            }

            b.Color = a.Color;
            b.Id = a.Id;
            b.Isbutton = a.ClickKey != 0;

            if (a.Font != null)
            {
                b.Font = GameFonts.Default;
            }

            b.Image = a.Image switch
            {
                "Solid" => null,
                null => null,
                _ => string.Concat(a.Image, ".png"),
            };

            s.widgets[i] = b;
        }

        // Auto-focus the first textbox widget.
        for (int i = 0; i < s.WidgetCount; i++)
        {
            if (s.widgets[i]?.Type == UIWidgetType.Textbox)
            {
                s.widgets[i].Editing = true;
                break;
            }
        }

        return s;
    }
}