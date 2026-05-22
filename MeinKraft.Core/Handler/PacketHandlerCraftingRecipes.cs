public class PacketHandlerCraftingRecipes(IGameWindowService gameService, IGame game) : ClientPacketHandler(gameService, game)
{
    internal ModGuiCrafting mod;

    public override void Handle(Packet_Server packet)
    {
        mod.d_CraftingRecipes = packet.CraftingRecipes.CraftingRecipes;
        mod.d_CraftingRecipesCount = packet.CraftingRecipes.CraftingRecipes.Length;
    }
}
