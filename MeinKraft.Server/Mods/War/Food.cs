namespace MeinKraft.Mods.War;

public class Food : IMod
{
    private IServerModManager? m;
    private int Cake;
    private int Apples;

    public void PreStart(IServerModManager m) => m.RequireMod("CoreBlocks");
    public void Start(IServerModManager manager, IModEvents modEvents)
    {
        m = manager;

        modEvents.BlockUse += OnUse;
        Cake = m.GetBlockId("Cake");
        Apples = m.GetBlockId("Apples");
    }

    private void OnUse(BlockUseArgs args)
    {
        if (m.GetBlock(args.X, args.Y, args.Z) == Cake || m.GetBlock(args.X, args.Y, args.Z) == Apples)
        {
            int health = m.GetPlayerHealth(args.Player);
            int maxhealth = m.GetPlayerMaxHealth(args.Player);

            health += 30;

            if (health > maxhealth)
                health = maxhealth;

            m.SetPlayerHealth(args.Player, health, maxhealth);
            m.SetBlock(args.X, args.Y, args.Z, 0);
        }
    }
}
