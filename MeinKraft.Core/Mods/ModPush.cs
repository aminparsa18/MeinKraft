using System.Numerics;


/// <summary>
/// Accumulates push forces from nearby explosion/push entities onto the local player each fixed frame.
/// </summary>
public class ModPush : ModBase
{

    public ModPush(IGame game) : base(game)
    {
    }

    public override void OnUpdate(float args)
    {
        Game.PushX = 0;
        Game.PushY = 0;
        Game.PushZ = 0;

        float pX = Game.Player.Position.X;
        float pY = Game.Player.Position.Y;
        float pZ = Game.Player.Position.Z;
        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Entity entity = Game.Entities[i];
            if (entity?.Push == null)
            {
                continue;
            }

            if (entity.NetworkPosition != null && !entity.NetworkPosition.PositionLoaded)
            {
                continue;
            }

            float kX = EncodingHelper.DecodeFixedPoint(entity.Push.XFloat);
            float kY = EncodingHelper.DecodeFixedPoint(entity.Push.ZFloat);
            float kZ = EncodingHelper.DecodeFixedPoint(entity.Push.YFloat);

            if (entity.Push.IsRelativeToPlayerPosition != 0)
            {
                kX += pX;
                kY += pY;
                kZ += pZ;
            }

            if (Vector3.Distance(new Vector3(kX, kY, kZ), new Vector3(pX, pY, pZ)) < EncodingHelper.DecodeFixedPoint(entity.Push.RangeFloat))
            {
                Game.PushX += pX - kX;
                Game.PushY += pY - kY;
                Game.PushZ += pZ - kZ;
            }
        }
    }
}