/// <summary>
/// Binds an <see cref="AnimatedModel"/> to the <see cref="TableSerializer"/>,
/// mapping tab-separated section data to and from model fields.
/// </summary>
public class AnimatedModelBinding : ITableBinding
{
    /// <summary>The model being populated or read.</summary>
    internal AnimatedModel m;

    /// <inheritdoc/>
    public void Set(string table, int index, string column, string value)
    {
        if (table == "nodes")
        {
            if (index >= m.Nodes.Count)
            {
                m.Nodes.Add(new Node());
            }

            if (m.Nodes[index] == null)
            {
                m.Nodes[index] = new Node();
            }

            Node k = m.Nodes[index];
            if (column == "name")
            {
                k.Name = value;
            }

            if (column == "paren")
            {
                k.ParentName = value;
            }

            if (column == "x")
            {
                k.PosX = IntParse(value);
            }

            if (column == "y")
            {
                k.PosY = IntParse(value);
            }

            if (column == "z")
            {
                k.PosZ = IntParse(value);
            }

            if (column == "rotx")
            {
                k.RotateX = IntParse(value);
            }

            if (column == "roty")
            {
                k.RotateY = IntParse(value);
            }

            if (column == "rotz")
            {
                k.RotateZ = IntParse(value);
            }

            if (column == "sizex")
            {
                k.SizeX = IntParse(value);
            }

            if (column == "sizey")
            {
                k.SizeY = IntParse(value);
            }

            if (column == "sizez")
            {
                k.SizeZ = IntParse(value);
            }

            if (column == "u")
            {
                k.U = IntParse(value);
            }

            if (column == "v")
            {
                k.V = IntParse(value);
            }

            if (column == "pivx")
            {
                k.PivotX = IntParse(value);
            }

            if (column == "pivy")
            {
                k.PivotY = IntParse(value);
            }

            if (column == "pivz")
            {
                k.PivotZ = IntParse(value);
            }

            if (column == "scalx")
            {
                k.ScaleX = IntParse(value);
            }

            if (column == "scaly")
            {
                k.ScaleY = IntParse(value);
            }

            if (column == "scalz")
            {
                k.ScaleZ = IntParse(value);
            }

            if (column == "head")
            {
                k.Head = IntParse(value);
            }
        }

        if (table == "keyframes")
        {
            while (m.Keyframes.Count <= index)
            {
                m.Keyframes.Add(new Keyframe());
            }

            Keyframe k = m.Keyframes[index];
            if (column == "anim")
            {
                k.AnimationName = value;
            }

            if (column == "node")
            {
                k.NodeName = value;
            }

            if (column == "frame")
            {
                k.Frame = IntParse(value);
            }

            if (column == "type")
            {
                k.Type = KeyframeTypeExtensions.FromSerializedName(value);
            }

            if (column == "x")
            {
                k.X = IntParse(value);
            }

            if (column == "y")
            {
                k.Y = IntParse(value);
            }

            if (column == "z")
            {
                k.Z = IntParse(value);
            }
        }

        if (table == "animations")
        {
            while (m.Animations.Count <= index)
            {
                m.Animations.Add(new Animation());
            }

            Animation k = m.Animations[index];
            if (column == "name")
            {
                k.Name = value;
            }

            if (column == "len")
            {
                k.Length = IntParse(value);
            }
        }

        if (table == "global")
        {
            if (column == "texw")
            {
                m.Global.TexW = IntParse(value);
            }

            if (column == "texh")
            {
                m.Global.TexH = IntParse(value);
            }
        }
    }

    /// <inheritdoc/>
    public void Get(string table, int index, Dictionary<string, string> items)
    {
        if (table == "nodes")
        {
            Node k = m.Nodes[index];
            items["name"] = k.Name;
            items["paren"] = k.ParentName;
            items["x"] = k.PosX.ToString();
            items["y"] = k.PosY.ToString();
            items["z"] = k.PosZ.ToString();
            items["rotx"] = k.RotateX.ToString();
            items["roty"] = k.RotateY.ToString();
            items["rotz"] = k.RotateZ.ToString();
            items["sizex"] = k.SizeX.ToString();
            items["sizey"] = k.SizeY.ToString();
            items["sizez"] = k.SizeZ.ToString();
            items["u"] = k.U.ToString();
            items["v"] = k.V.ToString();
            items["pivx"] = k.PivotX.ToString();
            items["pivy"] = k.PivotY.ToString();
            items["pivz"] = k.PivotZ.ToString();
            items["scalx"] = k.ScaleX.ToString();
            items["scaly"] = k.ScaleY.ToString();
            items["scalz"] = k.ScaleZ.ToString();
            items["head"] = k.Head.ToString();
        }

        if (table == "keyframes")
        {
            Keyframe k = m.Keyframes[index];
            items["anim"] = k.AnimationName;
            items["node"] = k.NodeName;
            items["frame"] = k.Frame.ToString();
            items["type"] = k.Type.ToSerializedName(); // was k.Type.ToString() — would break round-trip
            items["x"] = k.X.ToString();
            items["y"] = k.Y.ToString();
            items["z"] = k.Z.ToString();
        }

        if (table == "animations")
        {
            Animation k = m.Animations[index];
            items["name"] = k.Name;
            items["len"] = k.Length.ToString();
        }

        if (table == "global")
        {
            items["texw"] = m.Global.TexW.ToString();
            items["texh"] = m.Global.TexH.ToString();
        }
    }

    /// <inheritdoc/>
    public void GetTables(string[] names, int[] counts)
    {
        names[0] = "nodes";
        counts[0] = m.Nodes.Count;
        names[1] = "keyframes";
        counts[1] = m.Keyframes.Count;
        names[2] = "animations";
        counts[2] = m.Animations.Count;
        names[3] = "global";
        counts[3] = 1;
    }

    /// <summary>Parses a string to int, as required by <see cref="IGameWindowService"/>.</summary>
    private static int IntParse(string s)
    {
        _ = int.TryParse(s, out int ret);
        return ret;
    }
}