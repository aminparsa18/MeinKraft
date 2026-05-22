using System.Collections.Specialized;

/// <summary>Holds all parameters needed to establish a connection to a server.</summary>
public class ConnectionData
{
    /// <summary>The display name the player will use on the server.</summary>
    public string Username { get; set; } = "gamer";

    /// <summary>The server's IP address or hostname.</summary>
    public string Ip { get; set; }

    /// <summary>The server's port number. Defaults to <c>25565</c> when not specified in the URI.</summary>
    public int Port { get; set; }

    /// <summary>Optional authentication token sent during the handshake.</summary>
    public string Auth { get; set; }

    /// <summary>The password to authenticate with if the server is password-protected.</summary>
    public string ServerPassword { get; set; }

    /// <summary>Whether the server requires a password to join.</summary>
    public bool IsServerPasswordProtected { get; set; }

    /// <summary>
    /// Parses a <see cref="Uri"/> into a <see cref="ConnectionData"/> instance.
    /// <para>Supported query parameters:</para>
    /// <list type="bullet">
    ///   <item><term>user</term><description>Overrides the default username.</description></item>
    ///   <item><term>auth</term><description>Authentication token.</description></item>
    ///   <item><term>serverPassword</term><description>Whether the server is password-protected.</description></item>
    /// </list>
    /// </summary>
    /// <param name="uri">The connection URI to parse.</param>
    /// <returns>A populated <see cref="ConnectionData"/> instance.</returns>
    public static ConnectionData FromUri(Uri uri)
    {
        NameValueCollection query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        return new ConnectionData
        {
            Ip = uri.Host,
            Port = uri.Port != -1 ? uri.Port : 25565,
            Username = query["user"] ?? "gamer",
            Auth = query["auth"],
            IsServerPasswordProtected = EncodingHelper.ReadBool(query["serverPassword"])
        };
    }
}