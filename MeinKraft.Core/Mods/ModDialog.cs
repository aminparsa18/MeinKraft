using MeinKraft;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// Handles rendering and input for in-game dialogs (normal and modal).
/// </summary>
public class ModDialog : ModBase
{
    private static readonly string[] Empty = [];
    private const string TypableChars = "abcdefghijklmnopqrstuvwxyz1234567890\t ";

    private readonly ClientPacketHandler packetHandler;
    private readonly IGameWindowService platform;

    public ModDialog(IGameWindowService platform, IGame game) : base(game)
    {
        this.platform = platform;
        packetHandler = new ClientPacketHandlerDialog(platform, game);
    }

    public override void OnRender2d(float deltaTime)
    {
        Game.PacketHandlers[(int)Packet_ServerIdEnum.Dialog] = packetHandler;
        DrawDialogs(Game);
    }

    internal void DrawDialogs(IGame game)
    {
        for (int i = 0; i < game.Dialogs.Count; i++)
        {
            VisibleDialog d = game.Dialogs[i];
            if (d == null)
            {
                continue;
            }

            d.Screen.screenx = (platform.CanvasWidth / 2) - (d.Value.Width / 2);
            d.Screen.screeny = (platform.CanvasHeight / 2) - (d.Value.Height / 2);
            d.Screen.DrawWidgets(game);
        }
    }

    public override void OnKeyPress(KeyPressEventArgs args)
    {
        if (Game.GuiState is not GameState.ModalDialog and not GameState.Normal)
        {
            return;
        }

        if (Game.IsTyping)
        {
            return;
        }

        ForEachDialog(d => d.Screen.OnKeyPress(args));

        for (int k = 0; k < Game.Dialogs.Count; k++)
        {
            VisibleDialog d = Game.Dialogs[k];
            if (d == null)
            {
                continue;
            }

            for (int i = 0; i < d.Value.Widgets.Length; i++)
            {
                Widget w = d.Value.Widgets[i];
                if (w == null)
                {
                    continue;
                }

                // Only typeable characters are handled here; special characters use KeyDown
                if (TypableChars.Contains(w.ClickKey) && args.KeyChar == w.ClickKey)
                {
                    Game.SendPacketClient(ClientPackets.DialogClick(w.Id, Empty, 0));
                    return;
                }
            }
        }
    }

    public override void OnKeyDown(KeyEventArgs args)
    {
        ForEachDialog(d => d.Screen.OnKeyDown(args));

        bool isEsc = args.KeyChar == (int)Keys.Escape;

        if (Game.GuiState == GameState.Normal && isEsc)
        {
            for (int i = 0; i < Game.Dialogs.Count; i++)
            {
                VisibleDialog d = Game.Dialogs[i];
                if (d == null)
                {
                    continue;
                }

                if (d.Value.IsModal)
                {
                    Game.Dialogs[i] = null;
                    return;
                }
            }

            args.Handled = true;
            return;
        }

        if (Game.GuiState == GameState.ModalDialog)
        {
            if (isEsc)
            {
                // Close all modal dialogs
                for (int i = 0; i < Game.Dialogs.Count; i++)
                {
                    if (Game.Dialogs[i]?.Value.IsModal == true)
                    {
                        Game.Dialogs[i] = null;
                    }
                }

                Game.SendPacketClient(ClientPackets.DialogClick("Esc", Empty, 0));
                Game.GuiStateBackToGame();
                args.Handled = true;
            }
            else if (args.KeyChar == Game.GetKey(Keys.Tab))
            {
                Game.SendPacketClient(ClientPackets.DialogClick("Tab", Empty, 0));
                args.Handled = true;
            }
        }
    }

    public override void OnKeyUp(KeyEventArgs args)
        => ForEachDialog(d => d.Screen.OnKeyUp(args));

    public override void OnMouseDown(MouseEventArgs args)
        => ForEachDialog(d => d.Screen.OnMouseDown(args));
    public override void OnMouseUp(MouseEventArgs args)
        => ForEachDialog(d => d.Screen.OnMouseUp(args));

    /// <summary>Iterates all non-null dialogs and applies an action to each.</summary>
    private void ForEachDialog(Action<VisibleDialog> action)
    {
        for (int i = 0; i < Game.Dialogs.Count; i++)
        {
            if (Game.Dialogs[i] != null)
            {
                action(Game.Dialogs[i]);
            }
        }
    }
}