using MeinKraft;

/// <summary>
/// Contract for managing connected clients and the server console pseudo-client.
/// </summary>
public interface IClientRegistry
{
    /// <summary>
    /// All currently connected clients keyed by their numeric client ID.
    /// </summary>
    Dictionary<int, ServerPlayer> Clients { get; set; }

    /// <summary>
    /// A pseudo-client representing the server console. Always present and never
    /// appears in <see cref="Clients"/>. Its ID is <see cref="GameConstants.ServerConsoleId"/>.
    /// </summary>
    ServerPlayer ServerConsoleClient { get; set; }

    /// <summary>
    /// Returns the next available client ID by finding the lowest integer not
    /// already present in <see cref="Clients"/>.
    /// </summary>
    int GenerateClientId();

    /// <summary>
    /// Returns the client with the given numeric ID, or <see langword="null"/> if
    /// no such client is connected. Also handles the server console ID.
    /// </summary>
    /// <param name="id">Numeric client ID, or <see cref="GameConstants.ServerConsoleId"/>.</param>
    ServerPlayer GetClient(int id);

    /// <summary>
    /// Returns the client whose <c>PlayerName</c> matches <paramref name="name"/>
    /// (case-insensitive), including the server console client.
    /// Returns <see langword="null"/> if no match is found.
    /// </summary>
    /// <param name="name">The player username to search for.</param>
    ServerPlayer GetClient(string name);

    /// <summary>
    /// Whether the <see cref="ServerClient"/> record has been modified since the
    /// last save and needs to be persisted.
    /// </summary>
    bool ServerClientNeedsSaving { get; set; }

    /// <summary>
    /// Persistent server-side client record (default spawn, group membership, etc.)
    /// loaded from and saved to the database.
    /// </summary>
    ServerRoster ServerClient { get; set; }

    /// <summary>
    /// The server's active ban list. Used during connection to reject banned
    /// usernames and IP addresses before they reach the game state.
    /// </summary>
    ServerBanlist BanList { get; set; }
}

/// <summary>
/// Manages connected clients and the server console pseudo-client.
/// </summary>
public class ClientRegistry : IClientRegistry
{
    /// <inheritdoc/>
    public Dictionary<int, ServerPlayer> Clients { get; set; } = [];

    /// <inheritdoc/>
    public ServerBanlist BanList { get; set; }

    /// <inheritdoc/>
    public ServerPlayer? ServerConsoleClient { get; set; } = new ServerPlayer()
    {
        Id = GameConstants.ServerConsoleId,
        PlayerName = "Server",
        QueryClient = false,
    };

    /// <inheritdoc/>
    public ServerPlayer GetClient(int id)
    {
        if (id == GameConstants.ServerConsoleId)
            return ServerConsoleClient;

        return Clients.TryGetValue(id, out ServerPlayer? value) ? value : null;
    }

    /// <inheritdoc/>
    public ServerPlayer GetClient(string name)
    {
        if (ServerConsoleClient!.PlayerName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
            return ServerConsoleClient;

        foreach (KeyValuePair<int, ServerPlayer> k in Clients)
        {
            if (k.Value.PlayerName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                return k.Value;
        }

        return null;
    }

    /// <inheritdoc/>
    public int GenerateClientId()
    {
        int i = 0;
        while (Clients.ContainsKey(i))
        {
            i++;
        }

        return i;
    }

    /// <summary>
    /// Assigns the given <paramref name="group"/> to the server console client.
    /// </summary>
    /// <param name="group">The group to assign.</param>
    public void AssignGroup(Group group) => ServerConsoleClient.AssignGroup(group);

    /// <inheritdoc/>
    public bool ServerClientNeedsSaving { get; set; }

    /// <inheritdoc/>
    public ServerRoster ServerClient { get; set; }
}