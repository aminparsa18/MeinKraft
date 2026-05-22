public class EntityHeading
{
    public static byte GetHeading(float posx, float posy, float targetx, float targety)
    {
        float deltaX = targetx - posx;
        float deltaY = targety - posy;
        //Angle to x-axis: cos(beta) = x / |length|
        double headingDeg = (360.0 / (2.0 * Math.PI) * Math.Acos(deltaX / Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)))) + 90.0;
        //Add 2 Pi if value is negative
        if (deltaY < 0)
        {
            headingDeg = -headingDeg - 180.0;
        }

        if (headingDeg < 0)
        {
            headingDeg += 360.0;
        }

        if (headingDeg > 360.0)
        {
            headingDeg -= 360.0;
        }
        //Convert to value between 0 and 255 and return
        return (byte)(headingDeg / 360.0 * 256.0);
    }
}
