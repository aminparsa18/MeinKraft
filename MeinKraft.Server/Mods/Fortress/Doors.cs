namespace MeinKraft.Mods;

public class Doors : IMod
{
    public void PreStart(IServerModManager m) => m.RequireMod("CoreBlocks");

    public void Start(IServerModManager manager, IModEvents modEvents)
    {
        m = manager;

        modEvents.BlockBuild += OnBuild;
        modEvents.BlockDelete += OnDelete;
        modEvents.BlockUse += OnUse;

        m.SetString("en", "DoorBottomClosed", "Closed Door");
        m.SetString("en", "DoorTopClosed", "Closed Door");
        m.SetString("en", "DoorBottomOpen", "Open Door");
        m.SetString("en", "DoorTopOpen", "Open Door");

        SoundSet sounds = new()
        {
            Walk = ["walk1", "walk2", "walk3", "walk4"],
            Break = ["destruct"],
            Build = ["build"],
            Clone = ["clone"],
        };

        m.SetBlockType(126, "DoorBottomClosed", new BlockType()
        {
            AllTextures = "DoorBottom",
            DrawType = DrawType.ClosedDoor,
            WalkableType = WalkableType.Solid,
            Sounds = sounds,
            IsUsable = true,
        });
        m.SetBlockType(127, "DoorTopClosed", new BlockType()
        {
            AllTextures = "DoorTop",
            DrawType = DrawType.ClosedDoor,
            WalkableType = WalkableType.Solid,
            Sounds = sounds,
            IsUsable = true,
            WhenPlayerPlacesGetsConvertedTo = 126,
        });
        m.SetBlockType(128, "DoorBottomOpen", new BlockType()
        {
            AllTextures = "DoorBottom",
            DrawType = DrawType.OpenDoorLeft,
            WalkableType = WalkableType.Empty,
            Sounds = sounds,
            IsUsable = true,
            WhenPlayerPlacesGetsConvertedTo = 126,
        });
        m.SetBlockType(129, "DoorTopOpen", new BlockType()
        {
            AllTextures = "DoorTop",
            DrawType = DrawType.OpenDoorLeft,
            WalkableType = WalkableType.Empty,
            Sounds = sounds,
            IsUsable = true,
            WhenPlayerPlacesGetsConvertedTo = 126,
        });

        m.AddToCreativeInventory("DoorBottomClosed");
        m.AddCraftingRecipe("DoorBottomClosed", 1, "OakWood", 2);
        m.AddCraftingRecipe("DoorBottomClosed", 1, "BirchWood", 2);
        m.AddCraftingRecipe("DoorBottomClosed", 1, "SpruceWood", 2);

        DoorBottomClosed = m.GetBlockId("DoorBottomClosed");
        DoorTopClosed = m.GetBlockId("DoorTopClosed");
        DoorBottomOpen = m.GetBlockId("DoorBottomOpen");
        DoorTopOpen = m.GetBlockId("DoorTopOpen");
    }

    private IServerModManager m;
    private int DoorBottomClosed;
    private int DoorTopClosed;
    private int DoorBottomOpen;
    private int DoorTopOpen;

    private void OnBuild(BlockBuildArgs args)
    {
        //Check if placed block is bottom part of door (no need for further checks as player only has this type of block)
        if (m.GetBlock(args.X, args.Y, args.Z) == DoorBottomClosed)
        {
            //check if block above is valid and empty
            if (m.IsValidPos(args.X, args.Y, args.Z + 1) && m.GetBlock(args.X, args.Y, args.Z + 1) == 0)
            {
                m.SetBlock(args.X, args.Y, args.Z + 1, DoorTopClosed);
            }
            //if not, try to move door down 1 block
            else if (m.IsValidPos(args.X, args.Y, args.Z - 1) && m.GetBlock(args.X, args.Y, args.Z - 1) == 0)
            {
                m.SetBlock(args.X, args.Y, args.Z, DoorTopClosed);
                m.SetBlock(args.X, args.Y, args.Z - 1, DoorBottomClosed);
            }
            //if this fails, give the player back the block he built (survival mode) and set current block to empty
            else
            {
                m.SetBlock(args.X, args.Y, args.Z, 0);
                m.GrabBlock(args.Player, DoorBottomClosed);
            }
        }
    }

    private void OnDelete(BlockDeleteArgs args)
    {
        if (m.IsValidPos(args.X, args.Y, args.Z + 1) && (m.GetBlock(args.X, args.Y, args.Z + 1) == DoorTopClosed || m.GetBlock(args.X, args.Y, args.Z + 1) == DoorTopOpen))
        {
            m.SetBlock(args.X, args.Y, args.Z + 1, 0);
        }

        if (m.IsValidPos(args.X, args.Y, args.Z - 1) && (m.GetBlock(args.X, args.Y, args.Z - 1) == DoorBottomOpen || m.GetBlock(args.X, args.Y, args.Z - 1) == DoorBottomClosed))
        {
            m.SetBlock(args.X, args.Y, args.Z - 1, 0);
        }
    }

    private void OnUse(BlockUseArgs args)
    {
        //Closed door - bottom part
        if (m.GetBlock(args.X, args.Y, args.Z) == DoorBottomClosed)
        {
            //check block above
            if (m.GetBlock(args.X, args.Y, args.Z + 1) == DoorTopClosed)
            {
                //Modify blocks if there is a door counterpart
                m.SetBlock(args.X, args.Y, args.Z, DoorBottomOpen);
                m.SetBlock(args.X, args.Y, args.Z + 1, DoorTopOpen);
            }
            else
            {
                //delete used block as it is a leftover
                m.SetBlock(args.X, args.Y, args.Z, 0);
                m.GrabBlock(args.Player, DoorBottomClosed);
            }
        }

        //Open door - bottom part
        else if (m.GetBlock(args.X, args.Y, args.Z) == DoorBottomOpen)
        {
            //check block above
            if (m.GetBlock(args.X, args.Y, args.Z + 1) == DoorTopOpen)
            {
                //Modify blocks if there is a door counterpart
                m.SetBlock(args.X, args.Y, args.Z, DoorBottomClosed);
                m.SetBlock(args.X, args.Y, args.Z + 1, DoorTopClosed);
            }
            else
            {
                //delete used block as it is a leftover
                m.SetBlock(args.X, args.Y, args.Z, 0);
                m.GrabBlock(args.Player, DoorBottomClosed);
            }
        }

        //Closed door - top part
        else if (m.GetBlock(args.X, args.Y, args.Z) == DoorTopClosed)
        {
            //check block under used one
            if (m.GetBlock(args.X, args.Y, args.Z - 1) == DoorBottomClosed)
            {
                m.SetBlock(args.X, args.Y, args.Z, DoorTopOpen);
                m.SetBlock(args.X, args.Y, args.Z - 1, DoorBottomOpen);
            }
            else
            {
                //delete used block as it is a leftover
                m.SetBlock(args.X, args.Y, args.Z, 0);
                m.GrabBlock(args.Player, DoorBottomClosed);
            }
        }

        //Open door - top part
        else if (m.GetBlock(args.X, args.Y, args.Z) == DoorTopOpen)
        {
            //check block under used one
            if (m.GetBlock(args.X, args.Y, args.Z - 1) == DoorBottomOpen)
            {
                m.SetBlock(args.X, args.Y, args.Z, DoorTopClosed);
                m.SetBlock(args.X, args.Y, args.Z - 1, DoorBottomClosed);
            }
            else
            {
                //delete used block as it is a leftover
                m.SetBlock(args.X, args.Y, args.Z, 0);
                m.GrabBlock(args.Player, DoorBottomClosed);
            }
        }
    }
}
