using OpenTK.Windowing.GraphicsLibraryFramework;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

public partial class Game
{
    // -------------------------------------------------------------------------
    // Mouse button events
    // -------------------------------------------------------------------------

    public void MouseDown(MouseEventArgs args)
    {
        int btn = args.Button;

        if (btn == (int)MouseButton.Left)
        {
            mouseLeft = true;
            MouseLeftClick = true;
        }

        if (btn == (int)MouseButton.Middle)
        {
            mouseMiddle = true;
        }

        if (btn == (int)MouseButton.Right)
        {
            mouseRight = true;
            mouserightclick = true;
        }

        for (int i = 0; i < ClientMods.Count; i++)
        {
            if (ClientMods[i] == null)
            {
                continue;
            }

            ClientMods[i].OnMouseDown(args);
        }

        if (mousePointerLockShouldBe)
        {
            gameService.RequestMousePointerLock();
            mouseDeltaX = 0;
            mouseDeltaY = 0;
        }

        InvalidVersionAllow();
    }

    public void MouseUp(MouseEventArgs args)
    {
        int btn = args.Button;

        if (btn == (int)MouseButton.Left)
        {
            mouseLeft = false;
            mouseleftdeclick = true;
        }

        if (btn == (int)MouseButton.Middle)
        {
            mouseMiddle = false;
        }

        if (btn == (int)MouseButton.Right)
        {
            mouseRight = false;
        }

        for (int i = 0; i < ClientMods.Count; i++)
        {
            if (ClientMods[i] == null)
            {
                continue;
            }

            ClientMods[i].OnMouseUp(args);
        }
    }

    // -------------------------------------------------------------------------
    // Mouse move / wheel
    // -------------------------------------------------------------------------

    public void MouseMove(MouseEventArgs e)
    {
        if (!e.Emulated || e.ForceUsage)
        {
            // Set position only for real MouseMove events.
            MouseCurrentX = e.X;
            MouseCurrentY = e.Y;
        }

        if (e.Emulated || e.ForceUsage)
        {
            // Accumulate delta only from emulated events (actual events negate previous ones).
            mouseDeltaX += e.MovementX;
            mouseDeltaY += e.MovementY;
        }

        for (int i = 0; i < ClientMods.Count; i++)
        {
            if (ClientMods[i] == null)
            {
                continue;
            }

            ClientMods[i].OnMouseMove(e);
        }
    }

    public void MouseWheelChanged(float delta)
    {
        if (KeyboardState[GetKey(Keys.LeftShift)])
        {
            if (CameraType == CameraType.Overhead)
            {
                OverHeadCameraDistance = Math.Clamp(OverHeadCameraDistance - delta, TPP_CAMERA_DISTANCE_MIN, TPP_CAMERA_DISTANCE_MAX);
            }

            if (CameraType == CameraType.Tpp)
            {
                TppCameraDistance = Math.Clamp(TppCameraDistance - delta, TPP_CAMERA_DISTANCE_MIN, TPP_CAMERA_DISTANCE_MAX);
            }
        }

        for (int i = 0; i < ClientMods.Count; i++)
        {
            if (ClientMods[i] == null)
            {
                continue;
            }

            ClientMods[i].OnMouseWheelChanged(delta);
        }
    }

    public void UpdateMouseViewportControl(float dt)
    {
        if (mouseSmoothing)
        {
            const float smoothing1 = 0.85f;
            const float smoothing2 = 0.8f;
            float scale = smoothing2 / (300f / 75);
            mouseSmoothingVelX = (mouseSmoothingVelX + (mouseDeltaX * scale)) * smoothing1;
            mouseSmoothingVelY = (mouseSmoothingVelY + (mouseDeltaY * scale)) * smoothing1;
        }
        else
        {
            mouseSmoothingVelX = mouseDeltaX;
            mouseSmoothingVelY = mouseDeltaY;
        }

        if (GuiState == GameState.Normal && enableCameraControl && gameService.Focused())
        {
            if (!OverheadCamera && gameService.IsMousePointerLocked())
            {
                float rotScale = rotationspeed / 75f;
                Player.Position.RotY += mouseSmoothingVelX * rotScale;
                Player.Position.RotX += mouseSmoothingVelY * rotScale;
                Player.Position.RotX = Math.Clamp(Player.Position.RotX,
                    (MathF.PI / 2) + (15 / 1000),
                    (MathF.PI / 2) + MathF.PI - (15 / 1000));
            }

            if (!OverheadCamera)
            {
                float touchScale = constRotationSpeed * (1f / 75);
                Player.Position.RotX += TouchOrientationDy * touchScale;
                Player.Position.RotY += TouchOrientationDx * touchScale;
                TouchOrientationDx = 0;
                TouchOrientationDy = 0;
            }

            if (CameraType == CameraType.Overhead && (mouseMiddle || mouseRight))
            {
                OverheadCameraK.TurnLeft(mouseDeltaX / 70);
                OverheadCameraK.TurnUp(mouseDeltaY / 3);
            }
        }

        mouseDeltaX = 0;
        mouseDeltaY = 0;
    }

    // -------------------------------------------------------------------------
    // Free mouse / pointer lock
    // -------------------------------------------------------------------------

    public bool GetFreeMouse() => OverheadCamera || !gameService.IsMousePointerLocked();

    public void SetFreeMouse(bool value)
    {
        mousePointerLockShouldBe = !value;
        if (value)
        {
            gameService.ExitMousePointerLock();
        }
        else
        {
            gameService.RequestMousePointerLock();
        }
    }

    // -------------------------------------------------------------------------
    // Touch events
    // -------------------------------------------------------------------------

    public void OnTouchStart(TouchEventArgs e)
    {
        InvalidVersionAllow();
        MouseCurrentX = e.GetX();
        MouseCurrentY = e.GetY();
        MouseLeftClick = true;

        for (int i = 0; i < ClientMods.Count; i++)
        {
            if (ClientMods[i] == null)
            {
                continue;
            }

            ClientMods[i].OnTouchStart(e);
            if (e.GetHandled())
            {
                return;
            }
        }
    }

    public void OnTouchMove(TouchEventArgs e)
    {
        for (int i = 0; i < ClientMods.Count; i++)
        {
            if (ClientMods[i] == null)
            {
                continue;
            }

            ClientMods[i].OnTouchMove(e);
            if (e.GetHandled())
            {
                return;
            }
        }
    }

    public void OnTouchEnd(TouchEventArgs e)
    {
        MouseCurrentX = 0;
        MouseCurrentY = 0;

        for (int i = 0; i < ClientMods.Count; i++)
        {
            if (ClientMods[i] == null)
            {
                continue;
            }

            ClientMods[i].OnTouchEnd(e);
            if (e.GetHandled())
            {
                return;
            }
        }
    }

    public static void OnBackPressed() { }

    // -------------------------------------------------------------------------
    // Keyboard events
    // -------------------------------------------------------------------------

    public void KeyUp(KeyEventArgs eKey)
    {
        KeyboardStateRaw[eKey.KeyChar] = false;

        for (int i = 0; i < ClientMods.Count; i++)
        {
            ClientMods[i].OnKeyUp(eKey);
            if (eKey.Handled)
            {
                return;
            }
        }

        KeyboardState[eKey.KeyChar] = false;

        if (eKey.KeyChar == GetKey(Keys.LeftShift) || eKey.KeyChar == GetKey(Keys.RightShift))
        {
            IsShiftPressed = false;
        }
    }

    public void KeyPress(KeyPressEventArgs eKeyChar)
    {
        for (int i = 0; i < ClientMods.Count; i++)
        {
            if (ClientMods[i] == null)
            {
                continue;
            }

            ClientMods[i].OnKeyPress(eKeyChar);
            if (eKeyChar.Handled)
            {
                return;
            }
        }
    }

    public void KeyDown(KeyEventArgs eKey)
    {
        KeyboardStateRaw[eKey.KeyChar] = true;

        if (GuiState != GameState.MapLoading)
        {
            foreach (IModBase mod in ClientMods)
            {
                mod.OnKeyDown(eKey);
                if (eKey.Handled)
                {
                    return;
                }
            }
        }

        KeyboardState[eKey.KeyChar] = true;
        InvalidVersionAllow();

        if (eKey.KeyChar == GetKey(Keys.LeftShift) || eKey.KeyChar == GetKey(Keys.RightShift))
        {
            IsShiftPressed = true;
        }

        // F6 outside of Normal state: reconnect if lagging or map loading.
        if (eKey.KeyChar == GetKey(Keys.F6))
        {
            float lagSeconds = (gameService.TimeMillisecondsFromStart - LastReceivedMilliseconds) / 1000;
            if (lagSeconds >= GameConstants.DISCONNECTED_ICON_AFTER_SECONDS || GuiState == GameState.MapLoading)
            {
                Reconnect();
            }
        }

        if (GuiState == GameState.Normal)
        {
            KeyDownNormal(eKey.KeyChar);
        }
        else if (GuiState == GameState.Inventory)
        {
            if (eKey.KeyChar == GetKey(Keys.B) || eKey.KeyChar == GetKey(Keys.Escape))
            {
                GuiStateBackToGame();
            }

            return;
        }

        else if (GuiState == GameState.MapLoading)
        {
            if (eKey.KeyChar == GetKey(Keys.Escape))
            {
                ExitToMainMenu();
            }
        }

        else if (GuiState == GameState.CraftingRecipes)
        {
            if (eKey.KeyChar == GetKey(Keys.Escape))
            {
                GuiStateBackToGame();
            }
        }
    }

    private void KeyDownNormal(int eKey)
    {
        const string strFreemoveNotAllowed = "You are not allowed to enable freemove.";

        if (eKey == GetKey(Keys.F1))
        {
            if (!AllowFreeMove)
            {
                AddChatLine(strFreemoveNotAllowed);
                return;
            }

            MoveSpeed = Basemovespeed * 1;
            AddChatLine("Move speed: 1x.");
        }

        if (eKey == GetKey(Keys.F2))
        {
            if (!AllowFreeMove)
            {
                AddChatLine(strFreemoveNotAllowed);
                return;
            }

            MoveSpeed = Basemovespeed * 10;
            AddChatLine(string.Format(Language.MoveSpeed(), "10"));
        }

        if (eKey == GetKey(Keys.F3))
        {
            if (!AllowFreeMove)
            {
                AddChatLine(strFreemoveNotAllowed);
                return;
            }

            StopPlayerMove = true;
            if (!Controls.FreeMove)
            {
                Controls.FreeMove = true;
                AddChatLine(Language.MoveFree());
            }
            else if (Controls.FreeMove && !Controls.NoClip)
            {
                Controls.NoClip = true;
                AddChatLine(Language.MoveFreeNoclip());
            }
            else
            {
                Controls.FreeMove = false;
                Controls.NoClip = false;
                AddChatLine(Language.MoveNormal());
            }
        }

        if (eKey == GetKey(Keys.I))
        {
            DrawBlockInfo = !DrawBlockInfo;
        }

        int playerx = (int)Player.Position.X;
        int playery = (int)Player.Position.Z;
        if (playerx >= 0 && playerx < _voxelMap.MapSizeX && playery >= 0 && playery < _voxelMap.MapSizeY)
        {
            performanceinfo["height"] = string.Format("height:{0}", _voxelMap.Heightmap.GetBlock(playerx, playery).ToString());
        }

        if (eKey == GetKey(Keys.F5))
        {
            CameraChange();
        }

        if (eKey == GetKey(Keys.Equal))
        {
            if (CameraType == CameraType.Overhead)
            {
                OverHeadCameraDistance -= 1;
            }
            else if (CameraType == CameraType.Tpp)
            {
                TppCameraDistance -= 1;
            }
        }

        if (eKey == GetKey(Keys.Minus) || eKey == GetKey(Keys.KeyPadSubtract))
        {
            if (CameraType == CameraType.Overhead)
            {
                OverHeadCameraDistance += 1;
            }
            else if (CameraType == CameraType.Tpp)
            {
                TppCameraDistance += 1;
            }
        }

        OverHeadCameraDistance = Math.Clamp(OverHeadCameraDistance, TPP_CAMERA_DISTANCE_MIN, TPP_CAMERA_DISTANCE_MAX);
        TppCameraDistance = Math.Clamp(TppCameraDistance, TPP_CAMERA_DISTANCE_MIN, TPP_CAMERA_DISTANCE_MAX);

        if (eKey == GetKey(Keys.F6))
        {
            RedrawAllBlocks();
        }

        if (eKey == (int)Keys.F8)
        {
            ToggleVsync();
            if (EnableLog == 0)
            {
                AddChatLine(Language.FrameRateVsync());
            }

            if (EnableLog == 1)
            {
                AddChatLine(Language.FrameRateUnlimited());
            }

            if (EnableLog == 2)
            {
                AddChatLine(Language.FrameRateLagSimulation());
            }
        }

        if (eKey == GetKey(Keys.Tab))
        {
            SendPacketClient(ClientPackets.SpecialKeyTabPlayerList());
        }

        if (eKey == GetKey(Keys.E))
        {
            KeyDownUse();
        }

        if (eKey == GetKey(Keys.O))
        {
            Respawn();
        }

        if (eKey == GetKey(Keys.L))
        {
            SendPacketClient(ClientPackets.SpecialKeySelectTeam());
        }

        if (eKey == GetKey(Keys.P))
        {
            SendPacketClient(ClientPackets.SpecialKeySetSpawn());
            PlayerPositionSpawnX = Player.Position.X;
            PlayerPositionSpawnY = Player.Position.Y;
            PlayerPositionSpawnZ = Player.Position.Z;
            Player.Position.X = (int)Player.Position.X + (1f / 2);
            Player.Position.Z = (int)Player.Position.Z + (1f / 2);
        }

        if (eKey == GetKey(Keys.F))
        {
            AddChatLine(string.Format(Language.FogDistance(), ((int)Config3d.ViewDistance).ToString()));
            OnResize();
        }

        if (eKey == GetKey(Keys.B))
        {
            ShowInventory();
            return;
        }

        HandleMaterialKeys(eKey);
    }

    private void KeyDownUse()
    {
        if (CurrentAttackedBlock != null)
        {
            int posX = CurrentAttackedBlock.Value.X;
            int posY = CurrentAttackedBlock.Value.Y;
            int posZ = CurrentAttackedBlock.Value.Z;
            int blocktype = _voxelMap.GetBlock(posX, posY, posZ);

            if (_blockRegistry.IsUsableBlock(blocktype))
            {
                if (_blockRegistry.IsRailTile(blocktype))
                {
                    Player.Position.X = posX + (1f / 2);
                    Player.Position.Y = posZ + 1;
                    Player.Position.Z = posY + (1f / 2);
                    Controls.FreeMove = false;
                }
                else
                {
                    SendSetBlock(posX, posY, posZ, PacketBlockSetMode.Use, 0, ActiveMaterial);
                }
            }
        }

        if (CurrentlyAttackedEntity != -1 && Entities[CurrentlyAttackedEntity].IsUsable)
        {
            OnUseEntityArgs args = new() { Id = CurrentlyAttackedEntity };
            foreach (IModBase t in ClientMods)
            {
                if (t == null)
                {
                    continue;
                }

                t.OnUseEntity(args);
            }

            SendPacketClient(ClientPackets.UseEntity(CurrentlyAttackedEntity));
        }
    }

    private void HandleMaterialKeys(int eKey)
    {
        if (eKey == GetKey(Keys.KeyPad1))
        {
            ActiveMaterial = 0;
        }

        if (eKey == GetKey(Keys.KeyPad2))
        {
            ActiveMaterial = 1;
        }

        if (eKey == GetKey(Keys.KeyPad3))
        {
            ActiveMaterial = 2;
        }

        if (eKey == GetKey(Keys.KeyPad4))
        {
            ActiveMaterial = 3;
        }

        if (eKey == GetKey(Keys.KeyPad5))
        {
            ActiveMaterial = 4;
        }

        if (eKey == GetKey(Keys.KeyPad6))
        {
            ActiveMaterial = 5;
        }

        if (eKey == GetKey(Keys.KeyPad7))
        {
            ActiveMaterial = 6;
        }

        if (eKey == GetKey(Keys.KeyPad8))
        {
            ActiveMaterial = 7;
        }

        if (eKey == GetKey(Keys.KeyPad9))
        {
            ActiveMaterial = 8;
        }

        if (eKey == GetKey(Keys.KeyPad0))
        {
            ActiveMaterial = 9;
        }
    }

    public int GetKey(Keys key)
    {
        if (options == null)
        {
            return (int)key;
        }

        if (options.Keys[(int)key] != 0)
        {
            return options.Keys[(int)key];
        }

        return (int)key;
    }
}