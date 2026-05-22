using MeinKraft;
using OpenTK.Mathematics;

/// <summary>
/// Removes entities when their lifetime expires, triggering grenade explosions where applicable.
/// </summary>
public class ModExpire : ModBase
{
    private readonly IBlockRegistry _blockRegistry;

    public ModExpire(IGame game, IBlockRegistry blockTypeRegistry) : base(game)
    {
        _blockRegistry = blockTypeRegistry;
    }

    public override void OnUpdate(float args)
    {
        float dt = args;
        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Entity entity = Game.Entities[i];
            if (entity?.Expires == null)
            {
                continue;
            }

            entity.Expires.TimeLeft -= dt;
            if (entity.Expires.TimeLeft > 0)
            {
                continue;
            }

            if (entity.Grenade != null)
            {
                GrenadeExplosion(i);
            }

            Game.Entities[i] = null;
        }
    }

    private void GrenadeExplosion(int grenadeEntityId)
    {
        Entity grenadeEntity = Game.Entities[grenadeEntityId];
        Sprite sprite = grenadeEntity.Sprite;
        Grenade grenade = grenadeEntity.Grenade;
        BlockType blockType = _blockRegistry.BlockTypes[grenade.Block];

        float posX = sprite.PositionX;
        float posY = sprite.PositionY;
        float posZ = sprite.PositionZ;

        Game.PlayAudioAt("grenadeexplosion.ogg", posX, posY, posZ);

        // Spawn explosion animation sprite
        Game.EntityAddLocal(new Entity
        {
            Sprite = new Sprite
            {
                Image = "ani5.png",
                PositionX = posX,
                PositionY = posY + 1,
                PositionZ = posZ,
                Size = 200,
                AnimationCount = 4
            },
            Expires = Expiry.Create(1)
        });

        // Spawn explosion push entity
        float explosionTime = blockType.ExplosionTime;
        float explosionRange = blockType.ExplosionRange;

        Game.EntityAddLocal(new Entity
        {
            Push = new Packet_ServerExplosion
            {
                XFloat = EncodingHelper.EncodeFixedPoint(posX),
                YFloat = EncodingHelper.EncodeFixedPoint(posZ),
                ZFloat = EncodingHelper.EncodeFixedPoint(posY),
                RangeFloat = (int)blockType.ExplosionRange,
                IsRelativeToPlayerPosition = 0,
                TimeFloat = (int)blockType.ExplosionTime
            },
            Expires = new Expiry { TimeLeft = explosionTime }
        });

        // Apply damage to local player based on distance
        float dist = Vector3.Distance(new Vector3(Game.Player.Position.X, Game.Player.Position.Y, Game.Player.Position.Z), new Vector3(posX, posY, posZ));
        float dmg = (1f - (dist / explosionRange)) * blockType.DamageBody;
        if (dmg > 0)
        {
            Game.ApplyDamageToPlayer((int)dmg, DeathReason.Explosion, grenade.SourcePlayer);
        }
    }
}