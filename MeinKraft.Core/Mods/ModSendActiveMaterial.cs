/// <summary>
/// Notifies the server when the player's active material slot changes.
/// </summary>
public class ModSendActiveMaterial : ModBase
{
    private int previousActiveMaterialBlock;

    public ModSendActiveMaterial(IGame game) : base(game)
    {
    }

    public override void OnUpdate(float args)
    {
        if (Game.IsSinglePlayer)
            return;

        int activeBlock = Game.Inventory.RightHand[Game.ActiveMaterial]?.BlockId ?? 0;

        if (activeBlock != previousActiveMaterialBlock)
        {
            Game.SendPacketClient(ClientPackets.ActiveMaterialSlot(Game.ActiveMaterial));
        }

        previousActiveMaterialBlock = activeBlock;
    }
}