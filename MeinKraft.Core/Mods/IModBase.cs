public interface IModBase : IDisposable
{
    // Logic
    void OnUpdate(float dt);
    void OnFrame(float dt);

    // Render
    void OnBeforeRender3d(float dt);
    void OnRender3d(float dt);
    void OnRender2d(float dt);

    // Input
    bool OnClientCommand(ClientCommandArgs args);
    void OnKeyDown(KeyEventArgs args);
    void OnKeyPress(KeyPressEventArgs args);
    void OnKeyUp(KeyEventArgs args);
    void OnMouseDown(MouseEventArgs args);
    void OnMouseUp(MouseEventArgs args);
    void OnMouseMove(MouseEventArgs args);
    void OnMouseWheelChanged(float args);
    void OnTouchStart(TouchEventArgs e);
    void OnTouchMove(TouchEventArgs e);
    void OnTouchEnd(TouchEventArgs e);

    // Entity interaction
    void OnUseEntity(OnUseEntityArgs e);
    void OnHitEntity(OnUseEntityArgs e);
}