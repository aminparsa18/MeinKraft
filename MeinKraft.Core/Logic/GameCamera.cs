using OpenTK.Mathematics;

public partial class Game
{
    // -------------------------------------------------------------------------
    // Camera mode
    // -------------------------------------------------------------------------

    public void SetCamera(CameraType type)
    {
        switch (type)
        {
            case CameraType.Fpp:
                CameraType = CameraType.Fpp;
                SetFreeMouse(false);
                EnableTppView = false;
                OverheadCamera = false;
                break;

            case CameraType.Tpp:
                CameraType = CameraType.Tpp;
                EnableTppView = true;
                break;

            default: // Overhead
                CameraType = CameraType.Overhead;
                OverheadCamera = true;
                SetFreeMouse(true);
                EnableTppView = true;
                PlayerDestination = new Vector3(Player.Position.X, Player.Position.Y, Player.Position.Z);
                break;
        }
    }

    public void CameraChange()
    {
        // Prevent switching camera mode when following an entity.
        if (Follow != null)
        {
            return;
        }

        switch (CameraType)
        {
            case CameraType.Fpp:
                CameraType = CameraType.Tpp;
                EnableTppView = true;
                break;

            case CameraType.Tpp:
                CameraType = CameraType.Overhead;
                OverheadCamera = true;
                SetFreeMouse(true);
                EnableTppView = true;
                PlayerDestination = new Vector3(Player.Position.X, Player.Position.Y, Player.Position.Z);
                break;

            case CameraType.Overhead:
                CameraType = CameraType.Fpp;
                SetFreeMouse(false);
                EnableTppView = false;
                OverheadCamera = false;
                break;

            default:
                throw new ArgumentException($"Camera type is unknown in: {nameof(Game)} - {nameof(CameraChange)}");
        }
    }

    // -------------------------------------------------------------------------
    // Camera block queries
    // -------------------------------------------------------------------------

    private bool WaterSwimmingCamera() => GetCameraBlock() == -1 || _blockRegistry.IsWater(GetCameraBlock());

    private bool LavaSwimmingCamera() => _blockRegistry.IsLava(GetCameraBlock());

    private int GetCameraBlock()
    {
        int bx = (int)MathF.Floor(CameraEye.X);
        int by = (int)MathF.Floor(CameraEye.Z);
        int bz = (int)MathF.Floor(CameraEye.Y);

        return _voxelMap.IsValidPos(bx, by, bz) ? _voxelMap.GetBlockValid(bx, by, bz) : 0;
    }
}