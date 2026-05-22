using MemoryPack;

namespace MeinKraft;

/// <summary>
/// A server-side entity in the world. Entities can be players, monsters,
/// signs, push zones, or other interactive objects. Each optional component
/// is non-null only when that behaviour is active for this entity.
/// </summary>
[MemoryPackable]
public partial class ServerEntity
{
    /// <summary>World-space position and orientation. Non-null for all moving entities.</summary>
    public ServerEntityPositionAndOrientation? Position { get; set; }

    /// <summary>Floating name tag displayed above the entity.</summary>
    public ServerEntityDrawName? DrawName { get; set; }

    /// <summary>3D animated model descriptor. Non-null for player and creature entities.</summary>
    public ServerEntityAnimatedModel? DrawModel { get; set; }

    /// <summary>Floating text label rendered in world space.</summary>
    public ServerEntityDrawText? DrawText { get; set; }

    /// <summary>Push zone that applies a force to nearby players.</summary>
    public ServerEntityPush? Push { get; set; }

    /// <summary>When <see langword="true"/>, players can right-click this entity to interact with it.</summary>
    public bool Usable { get; set; }

    /// <summary>Visible area zone, optionally restricted to a single client.</summary>
    public ServerEntityDrawArea? DrawArea { get; set; }

    /// <summary>Sign text attached to this entity.</summary>
    public ServerEntitySign? Sign { get; set; }

    /// <summary>Permission sign that grants or restricts access to an area.</summary>
    public ServerEntityPermissionSign? PermissionSign { get; set; }
}

// ── Components ────────────────────────────────────────────────────────────────

/// <summary>
/// Defines a visible rectangular area zone for this entity,
/// optionally visible only to a specific client.
/// </summary>
[MemoryPackable]
public partial class ServerEntityDrawArea
{
    /// <summary>World X coordinate of the zone's origin.</summary>
    public int X { get; set; }

    /// <summary>World Y coordinate of the zone's origin.</summary>
    public int Y { get; set; }

    /// <summary>World Z coordinate of the zone's origin.</summary>
    public int Z { get; set; }

    /// <summary>Width of the zone in blocks.</summary>
    public int SizeX { get; set; }

    /// <summary>Depth of the zone in blocks.</summary>
    public int SizeY { get; set; }

    /// <summary>Height of the zone in blocks.</summary>
    public int SizeZ { get; set; }

    /// <summary>
    /// Client ID that can see this zone, or <c>0</c> to make it visible to all clients.
    /// </summary>
    public int VisibleToClientId { get; set; }
}

/// <summary>A floating name tag rendered above the entity in the world.</summary>
[MemoryPackable]
public partial class ServerEntityDrawName
{
    /// <summary>The text shown in the name tag.</summary>
    public string? Name { get; set; }

    /// <summary>When <see langword="true"/>, the tag is only shown when the entity is selected/targeted.</summary>
    public bool OnlyWhenSelected { get; set; }

    /// <summary>When <see langword="true"/>, the client can auto-complete this name in chat.</summary>
    public bool ClientAutoComplete { get; set; }

    /// <summary>HTML-style colour code applied to the name text (e.g. <c>"#FF0000"</c>).</summary>
    public string? Color { get; set; }
}

/// <summary>A text sign attached to an entity.</summary>
[MemoryPackable]
public partial class ServerEntitySign
{
    /// <summary>The text displayed on the sign.</summary>
    public string? Text { get; set; }
}

/// <summary>
/// A permission sign that grants or restricts access to an area
/// based on player name or group membership.
/// </summary>
[MemoryPackable]
public partial class ServerEntityPermissionSign
{
    /// <summary>Player name or group name this sign applies to.</summary>
    public string? Name { get; set; }

    /// <summary>Whether this sign targets an individual player or a named group.</summary>
    public PermissionSignType Type { get; set; }
}

/// <summary>Controls whether a permission sign applies to a player or a group.</summary>
public enum PermissionSignType
{
    /// <summary>Applies to a specific named player.</summary>
    Player,

    /// <summary>Applies to all members of a named group.</summary>
    Group,
}

/// <summary>Describes the 3D animated model used to render this entity.</summary>
[MemoryPackable]
public partial class ServerEntityAnimatedModel
{
    /// <summary>Asset name of the model file (e.g. <c>"player.obj"</c>).</summary>
    public string? Model { get; set; }

    /// <summary>Asset name of the texture applied to the model.</summary>
    public string? Texture { get; set; }

    /// <summary>Height of the camera eye point above the entity's feet, in blocks.</summary>
    public float EyeHeight { get; set; }

    /// <summary>Total model height in blocks, used for collision and camera calculations.</summary>
    public float ModelHeight { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the client should attempt to download
    /// a custom player skin for this entity.
    /// </summary>
    public bool DownloadSkin { get; set; }
}

/// <summary>World-space position and orientation of a server entity.</summary>
[MemoryPackable]
public partial class ServerEntityPositionAndOrientation
{
    /// <summary>World X coordinate in blocks.</summary>
    public float X { get; set; }

    /// <summary>World Y coordinate in blocks.</summary>
    public float Y { get; set; }

    /// <summary>World Z coordinate in blocks.</summary>
    public float Z { get; set; }

    /// <summary>Horizontal rotation (yaw) encoded as a 256-step byte (0–255 = 0°–360°).</summary>
    public byte Heading { get; set; }

    /// <summary>Vertical rotation (pitch) encoded as a 256-step byte.</summary>
    public byte Pitch { get; set; }

    /// <summary>Stance byte (standing, crouching, etc.).</summary>
    public byte Stance { get; set; }

    /// <summary>Returns a shallow copy of this position snapshot.</summary>
    public ServerEntityPositionAndOrientation Clone() => new()
    {
        X = X,
        Y = Y,
        Z = Z,
        Heading = Heading,
        Pitch = Pitch,
        Stance = Stance,
    };
}

/// <summary>A floating text label rendered at a world-space offset from the entity.</summary>
[MemoryPackable]
public partial class ServerEntityDrawText
{
    /// <summary>The text to display.</summary>
    public string? Text { get; set; }

    /// <summary>X offset from the entity's position in blocks.</summary>
    public float Dx { get; set; }

    /// <summary>Y offset from the entity's position in blocks.</summary>
    public float Dy { get; set; }

    /// <summary>Z offset from the entity's position in blocks.</summary>
    public float Dz { get; set; }

    /// <summary>Rotation around the X axis in radians.</summary>
    public float RotX { get; set; }

    /// <summary>Rotation around the Y axis in radians.</summary>
    public float RotY { get; set; }

    /// <summary>Rotation around the Z axis in radians.</summary>
    public float RotZ { get; set; }
}

/// <summary>
/// A push zone that applies a directional force to any player entering its range.
/// </summary>
[MemoryPackable]
public partial class ServerEntityPush
{
    /// <summary>Radius in blocks within which the push force is applied.</summary>
    public float Range { get; set; }
}