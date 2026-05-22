using System.Text;

/// <summary>
/// Lightweight response container for async HTTP requests.
/// Signals completion or failure and holds the raw response bytes.
/// </summary>
public class HttpResponse
{
    public bool Done { get; set; }
    public bool Error { get; set; }
    public byte[] Value { get; set; }
    public string GetString()
        => Encoding.UTF8.GetString(Value, 0, Value.Length);
}