public class EncodingHelper
{
    public static string CharArrayToString(int[] charArray, int length)
        => new(Array.ConvertAll(charArray, c => (char)c), 0, length);

    public static bool ReadBool(string str)
    {
        return str != null && str != "0"
                && (!str.Equals(bool.FalseString, StringComparison.InvariantCultureIgnoreCase));
    }

    public static bool IsValidTypingChar(int c_)
    {
        char c = (char)c_;
        return !char.IsControl(c) && c != '\t' && c != '\r';
    }

    public static bool IsChecksum(string checksum)
    {
        //Check if checksum string has correct length
        if (checksum.Length != 32)
        {
            return false;
        }
        //Convert checksum string to lowercase letters
        checksum = checksum.ToLower();
        char[] chars = checksum.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] is (< '0' or > '9') and (< 'a' or > 'f'))
            {
                //Return false if any character inside the checksum is not hexadecimal
                return false;
            }
        }
        //Return true if all checks have been passed
        return true;
    }

    public static string DecodeHTMLEntities(string htmlEncodedString) => System.Web.HttpUtility.HtmlDecode(htmlEncodedString);

    /// <summary>Encodes a yaw angle (radians) as a 0–255 byte.</summary>
    public static byte HeadingByte(float orientationX, float orientationY, float orientationZ)
        => (byte)(int)(orientationY % (2 * MathF.PI) / (2 * MathF.PI) * 256);

    /// <summary>Encodes a pitch angle (radians) as a 0–255 byte.</summary>
    public static byte PitchByte(float orientationX, float orientationY, float orientationZ)
    {
        float xx = (orientationX + MathF.PI) % (2 * MathF.PI);
        return (byte)(int)(xx / (2 * MathF.PI) * 256);
    }

    /// <summary>Decodes a Q5 fixed-point integer (value / 32) to a float.</summary>
    public static float DecodeFixedPoint(int value) => value / 32f;

    /// <summary>Encodes a float to Q5 fixed-point (value × 32).</summary>
    public static int EncodeFixedPoint(float p) => (int)(p * 32);
}