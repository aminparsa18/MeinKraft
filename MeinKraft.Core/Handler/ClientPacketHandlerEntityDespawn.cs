/// <summary>
/// Handles <see cref="Packet_ServerIdEnum.EntityDespawn"/> packets,
/// cleaning up downloaded player skin textures before removing the entity.
/// </summary>
public class ClientPacketHandlerEntityDespawn(IGameWindowService gameService, IGame game) : ClientPacketHandler(gameService, game)
{
    public override void Handle(Packet_Server packet)
    {
        int id = packet.EntityDespawn.Id;
        Entity entity = game.Entities[id];

        // Clean up a downloaded player skin if one was loaded for this entity.
        if (entity?.DrawModel?.DownloadSkin == true)
        {
            int currentTex = entity.DrawModel.CurrentTexture;
            if (currentTex > 0 && currentTex != game.GetTexture("mineplayer.png"))
            {
                entity.DrawModel.CurrentTexture = -1;
                game.DeleteTexture(entity.DrawName.Name);
            }
        }

        game.Entities[id] = null;
    }
}