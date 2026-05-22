public class DirectionUtils
{
    /// <summary>
    /// VehicleDirection12.UpRightRight -> returns Direction4.Right
    /// </summary>
    /// <param name="direction"></param>
    /// <returns></returns>
    public static TileExitDirection ResultExit(VehicleDirection12 direction)
    {
        return direction switch
        {
            VehicleDirection12.HorizontalLeft => TileExitDirection.Left,
            VehicleDirection12.HorizontalRight => TileExitDirection.Right,
            VehicleDirection12.VerticalUp => TileExitDirection.Up,
            VehicleDirection12.VerticalDown => TileExitDirection.Down,
            VehicleDirection12.UpLeftUp => TileExitDirection.Up,
            VehicleDirection12.UpLeftLeft => TileExitDirection.Left,
            VehicleDirection12.UpRightUp => TileExitDirection.Up,
            VehicleDirection12.UpRightRight => TileExitDirection.Right,
            VehicleDirection12.DownLeftDown => TileExitDirection.Down,
            VehicleDirection12.DownLeftLeft => TileExitDirection.Left,
            VehicleDirection12.DownRightDown => TileExitDirection.Down,
            VehicleDirection12.DownRightRight => TileExitDirection.Right,
            _ => TileExitDirection.Down,
        };
    }

    public static RailDirection ToRailDirection(VehicleDirection12 direction)
    {
        return direction switch
        {
            VehicleDirection12.HorizontalLeft => RailDirection.Horizontal,
            VehicleDirection12.HorizontalRight => RailDirection.Horizontal,
            VehicleDirection12.VerticalUp => RailDirection.Vertical,
            VehicleDirection12.VerticalDown => RailDirection.Vertical,
            VehicleDirection12.UpLeftUp => RailDirection.UpLeft,
            VehicleDirection12.UpLeftLeft => RailDirection.UpLeft,
            VehicleDirection12.UpRightUp => RailDirection.UpRight,
            VehicleDirection12.UpRightRight => RailDirection.UpRight,
            VehicleDirection12.DownLeftDown => RailDirection.DownLeft,
            VehicleDirection12.DownLeftLeft => RailDirection.DownLeft,
            VehicleDirection12.DownRightDown => RailDirection.DownRight,
            VehicleDirection12.DownRightRight => RailDirection.DownRight,
            _ => RailDirection.DownLeft,
        };
    }

    public static int ToRailDirectionFlags(RailDirection direction)
    {
        return direction switch
        {
            RailDirection.DownLeft => (int)RailDirectionFlags.DownLeft,
            RailDirection.DownRight => (int)RailDirectionFlags.DownRight,
            RailDirection.Horizontal => (int)RailDirectionFlags.Horizontal,
            RailDirection.UpLeft => (int)RailDirectionFlags.UpLeft,
            RailDirection.UpRight => (int)RailDirectionFlags.UpRight,
            RailDirection.Vertical => (int)RailDirectionFlags.Vertical,
            _ => 0,
        };
    }

    public static VehicleDirection12 Reverse(VehicleDirection12 direction)
    {
        return direction switch
        {
            VehicleDirection12.HorizontalLeft => VehicleDirection12.HorizontalRight,
            VehicleDirection12.HorizontalRight => VehicleDirection12.HorizontalLeft,
            VehicleDirection12.VerticalUp => VehicleDirection12.VerticalDown,
            VehicleDirection12.VerticalDown => VehicleDirection12.VerticalUp,
            VehicleDirection12.UpLeftUp => VehicleDirection12.UpLeftLeft,
            VehicleDirection12.UpLeftLeft => VehicleDirection12.UpLeftUp,
            VehicleDirection12.UpRightUp => VehicleDirection12.UpRightRight,
            VehicleDirection12.UpRightRight => VehicleDirection12.UpRightUp,
            VehicleDirection12.DownLeftDown => VehicleDirection12.DownLeftLeft,
            VehicleDirection12.DownLeftLeft => VehicleDirection12.DownLeftDown,
            VehicleDirection12.DownRightDown => VehicleDirection12.DownRightRight,
            VehicleDirection12.DownRightRight => VehicleDirection12.DownRightDown,
            _ => VehicleDirection12.DownLeftDown,
        };
    }

    public static int ToVehicleDirection12Flags(VehicleDirection12 direction)
    {
        return direction switch
        {
            VehicleDirection12.HorizontalLeft => VehicleDirection12Flags.HorizontalLeft,
            VehicleDirection12.HorizontalRight => VehicleDirection12Flags.HorizontalRight,
            VehicleDirection12.VerticalUp => VehicleDirection12Flags.VerticalUp,
            VehicleDirection12.VerticalDown => VehicleDirection12Flags.VerticalDown,
            VehicleDirection12.UpLeftUp => VehicleDirection12Flags.UpLeftUp,
            VehicleDirection12.UpLeftLeft => VehicleDirection12Flags.UpLeftLeft,
            VehicleDirection12.UpRightUp => VehicleDirection12Flags.UpRightUp,
            VehicleDirection12.UpRightRight => VehicleDirection12Flags.UpRightRight,
            VehicleDirection12.DownLeftDown => VehicleDirection12Flags.DownLeftDown,
            VehicleDirection12.DownLeftLeft => VehicleDirection12Flags.DownLeftLeft,
            VehicleDirection12.DownRightDown => VehicleDirection12Flags.DownRightDown,
            VehicleDirection12.DownRightRight => VehicleDirection12Flags.DownRightRight,
            _ => 0,
        };
    }

    public static TileEnterDirection ResultEnter(TileExitDirection direction)
    {
        return direction switch
        {
            TileExitDirection.Up => TileEnterDirection.Down,
            TileExitDirection.Down => TileEnterDirection.Up,
            TileExitDirection.Left => TileEnterDirection.Right,
            TileExitDirection.Right => TileEnterDirection.Left,
            _ => TileEnterDirection.Down,
        };
    }

    public static int RailDirectionFlagsCount(int railDirectionFlags)
    {
        int count = 0;
        if ((railDirectionFlags & ToRailDirectionFlags(RailDirection.DownLeft)) != 0)
        {
            count++;
        }

        if ((railDirectionFlags & ToRailDirectionFlags(RailDirection.DownRight)) != 0)
        {
            count++;
        }

        if ((railDirectionFlags & ToRailDirectionFlags(RailDirection.Horizontal)) != 0)
        {
            count++;
        }

        if ((railDirectionFlags & ToRailDirectionFlags(RailDirection.UpLeft)) != 0)
        {
            count++;
        }

        if ((railDirectionFlags & ToRailDirectionFlags(RailDirection.UpRight)) != 0)
        {
            count++;
        }

        if ((railDirectionFlags & ToRailDirectionFlags(RailDirection.Vertical)) != 0)
        {
            count++;
        }

        return count;
    }

    public static int ToVehicleDirection12Flags_(VehicleDirection12[] directions, int directionsCount)
    {
        int flags = VehicleDirection12Flags.None;
        for (int i = 0; i < directionsCount; i++)
        {
            VehicleDirection12 d = directions[i];
            flags |= ToVehicleDirection12Flags(d);
        }

        return flags;
    }

    /// <summary>
    /// Enter at TileEnterDirection.Left -> yields VehicleDirection12.UpLeftUp,
    /// VehicleDirection12.HorizontalRight,
    /// VehicleDirection12.DownLeftDown
    /// </summary>
    /// <param name="enter_at"></param>
    /// <returns></returns>
    public static VehicleDirection12[]? PossibleNewRails3(TileEnterDirection enter_at)
    {
        VehicleDirection12[] ret = new VehicleDirection12[3];
        switch (enter_at)
        {
            case TileEnterDirection.Left:
                ret[0] = VehicleDirection12.UpLeftUp;
                ret[1] = VehicleDirection12.HorizontalRight;
                ret[2] = VehicleDirection12.DownLeftDown;
                break;
            case TileEnterDirection.Down:
                ret[0] = VehicleDirection12.DownLeftLeft;
                ret[1] = VehicleDirection12.VerticalUp;
                ret[2] = VehicleDirection12.DownRightRight;
                break;
            case TileEnterDirection.Up:
                ret[0] = VehicleDirection12.UpLeftLeft;
                ret[1] = VehicleDirection12.VerticalDown;
                ret[2] = VehicleDirection12.UpRightRight;
                break;
            case TileEnterDirection.Right:
                ret[0] = VehicleDirection12.UpRightUp;
                ret[1] = VehicleDirection12.HorizontalLeft;
                ret[2] = VehicleDirection12.DownRightDown;
                break;
            default:
                return null;
        }

        return ret;
    }
}

public class VehicleDirection12Flags
{
    public const int None = 0;
    public const int HorizontalLeft = 1 << 0;
    public const int HorizontalRight = 1 << 1;
    public const int VerticalUp = 1 << 2;
    public const int VerticalDown = 1 << 3;

    public const int UpLeftUp = 1 << 4;
    public const int UpLeftLeft = 1 << 5;
    public const int UpRightUp = 1 << 6;
    public const int UpRightRight = 1 << 7;

    public const int DownLeftDown = 1 << 8;
    public const int DownLeftLeft = 1 << 9;
    public const int DownRightDown = 1 << 10;
    public const int DownRightRight = 1 << 11;
}