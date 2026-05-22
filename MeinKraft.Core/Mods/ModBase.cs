using OpenTK.Windowing.Common;

/// <summary>
/// Base class for all client-side mods. Override only the hooks you need.
///
/// Threading is not your concern here — the game loop calls these hooks
/// on the correct thread at the correct time. Background work (chunk
/// tessellation, world gen) goes through <see cref="IChunkWorkQueue"/>,
/// not through mod hooks.
/// </summary>
public abstract class ModBase(IGame game) : IModBase
{
    protected IGame Game = game;

    // ── Logic ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called once per fixed timestep tick (75 Hz).
    /// Use for physics, movement, game logic — anything that must be
    /// frame-rate independent.
    /// </summary>
    public virtual void OnUpdate(float dt) { }

    /// <summary>
    /// Called once per render frame (variable rate).
    /// Use for anything that should track real elapsed time rather than
    /// the fixed tick, such as animations or UI transitions.
    /// </summary>
    public virtual void OnFrame(float dt) { }

    // ── Render ────────────────────────────────────────────────────────────────

    /// <summary>Called before the 3D draw pass. Use to set up camera or GL state.</summary>
    public virtual void OnBeforeRender3d(float dt) { }

    /// <summary>Called during the 3D draw pass. Depth test is active.</summary>
    public virtual void OnRender3d(float dt) { }

    /// <summary>Called during the 2D overlay pass. Projection is orthographic.</summary>
    public virtual void OnRender2d(float dt) { }

    // ── Input ─────────────────────────────────────────────────────────────────

    public virtual bool OnClientCommand(ClientCommandArgs args) => false;

    public virtual void OnKeyDown(KeyEventArgs args) { }
    public virtual void OnKeyPress(KeyPressEventArgs args) { }
    public virtual void OnKeyUp(KeyEventArgs args) { }

    public virtual void OnMouseDown(MouseEventArgs args) { }
    public virtual void OnMouseUp(MouseEventArgs args) { }
    public virtual void OnMouseMove(MouseEventArgs args) { }
    public virtual void OnMouseWheelChanged(float args) { }

    public virtual void OnTouchStart(TouchEventArgs e) { }
    public virtual void OnTouchMove(TouchEventArgs e) { }
    public virtual void OnTouchEnd(TouchEventArgs e) { }

    // ── Entity interaction ────────────────────────────────────────────────────

    public virtual void OnUseEntity(OnUseEntityArgs e) { }
    public virtual void OnHitEntity(OnUseEntityArgs e) { }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public virtual void Dispose() { }
}