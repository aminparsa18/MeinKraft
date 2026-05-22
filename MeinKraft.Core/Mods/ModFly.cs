using static MeinKraft.Mods.ModNetworkProcess;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// ModFly – client-side flight mod.
///   F      → toggle flight mode on / off
///   Space  → rise  (while in flight mode)
///   Shift  → sink  (while in flight mode)
///
/// Works by driving Controls.freemove / moveup / movedown directly,
/// which ScriptCharacterPhysics already understands:
///   - freemove=true  → gravity disabled, full 3-axis movement
///   - moveup=true    → ascend (same as swim-up)
///   - movedown=true  → descend
/// </summary>
public class ModFly : ModBase
{

    private bool flyActive = false;

    public ModFly(IGame game) : base(game)
    {
    }

    // ── Toggle on F ───────────────────────────────────────────────────────────
    public override void OnKeyDown(KeyEventArgs args)
    {
        if (Game.GuiState != GameState.Normal || Game.GuiTyping != TypingState.None)
        {
            return;
        }

        if (args.KeyChar != Game.GetKey(Keys.F))
        {
            return;
        }

        flyActive = !flyActive;
        if (flyActive)
        {
            Game.AddChatLine("&aFlight ON  –  Space: rise  |  Shift: sink  |  F: exit");
        }
        else
        {
            // Clear fly controls so physics resumes normally on the very next tick
            Game.Controls.FreeMove = false;
            Game.Controls.MoveUp = false;
            Game.Controls.MoveDown = false;
            Game.playervelocity = new OpenTK.Mathematics.Vector3(
                Game.playervelocity.X, 0f, Game.playervelocity.Z);
            Game.AddChatLine("&cFlight OFF");
        }
    }

    // ── Feed freemove + vertical intent into Controls every physics tick ──────
    public override void OnUpdate(float dt)
    {
        if (!flyActive)
        {
            return;
        }

        Game.Controls.FreeMove = true;

        bool space = Game.KeyboardStateRaw[Game.GetKey(Keys.Space)];
        bool shift = Game.KeyboardStateRaw[Game.GetKey(Keys.LeftControl)];

        // Both or neither → hover (moveup and movedown both false keeps Y still)
        Game.Controls.MoveUp = space && !shift;
        Game.Controls.MoveDown = shift && !space;
    }

    // ── Cleanup on mod unload ─────────────────────────────────────────────────
    public override void Dispose()
    {
        if (!flyActive)
        {
            return;
        }

        Game.Controls.FreeMove = false;
        Game.Controls.MoveUp = false;
        Game.Controls.MoveDown = false;
    }
}