using static MeinKraft.Mods.ModNetworkProcess;

public partial class Game
{
    public bool IsTeamchat { get; set; }

    // -------------------------------------------------------------------------
    // Packet serialization / sending
    // -------------------------------------------------------------------------

    private static byte[] Serialize(Packet_Client packet) => MemoryPackSerializer.Serialize(packet);

    private void SendPacket(byte[] packet) => NetClient.SendMessage(packet.AsMemory(0, packet.Length), MyNetDeliveryMethod.ReliableOrdered);

    public void SendPacketClient(Packet_Client packetClient) => SendPacket(Serialize(packetClient));

    // -------------------------------------------------------------------------
    // Game actions → packets
    // -------------------------------------------------------------------------

    private void SendChat(string s) => SendPacketClient(ClientPackets.Chat(s, IsTeamchat ? 1 : 0));

    public void SendPingReply() => SendPacketClient(ClientPackets.PingReply());

    public void SendSetBlock(int x, int y, int z, PacketBlockSetMode mode, int type, int materialslot) => SendPacketClient(ClientPackets.SetBlock(x, y, z, mode, type, materialslot));

    public void SendFillArea(int startx, int starty, int startz, int endx, int endy, int endz, int blockType) => SendPacketClient(ClientPackets.FillArea(startx, starty, startz, endx, endy, endz, blockType, ActiveMaterial));

    private void SendRequestBlob(string[] required, int requiredCount) => SendPacketClient(ClientPackets.RequestBlob(required));

    private void SendGameResolution() => SendPacketClient(ClientPackets.GameResolution(gameService.CanvasWidth, gameService.CanvasHeight));

    public void SendLeave(PacketLeaveReason reason) => SendPacketClient(ClientPackets.Leave(reason));

    private void Respawn()
    {
        SendPacketClient(ClientPackets.SpecialKeyRespawn());
        StopPlayerMove = true;
    }

    // -------------------------------------------------------------------------
    // Inventory actions → packets
    // -------------------------------------------------------------------------

    public void InventoryClick(Packet_InventoryPosition pos) => SendPacketClient(ClientPackets.InventoryClick(pos));

    public void WearItem(Packet_InventoryPosition from, Packet_InventoryPosition to) => SendPacketClient(ClientPackets.WearItem(from, to));

    public void MoveToInventory(Packet_InventoryPosition from) => SendPacketClient(ClientPackets.MoveToInventory(from));

    // -------------------------------------------------------------------------
    // Connection
    // -------------------------------------------------------------------------

    private void Connect()
    {
        if (string.IsNullOrEmpty(ConnectData.ServerPassword))
        {
            Connect(ConnectData.Ip, ConnectData.Port, ConnectData.Username, ConnectData.Auth);
        }
        else
        {
            Connect(ConnectData.Ip, ConnectData.Port, ConnectData.Username, ConnectData.Auth, ConnectData.ServerPassword);
        }

        MapLoadingStart();
    }

    private void Connect(string serverAddress, int port, string username, string auth)
    {
        NetClient.Start();
        NetClient.Connect(serverAddress, port);
        SendPacketClient(ClientPackets.CreateLoginPacket(username, auth));
    }

    private void Connect(string serverAddress, int port, string username, string auth, string serverPassword)
    {
        NetClient.Start();
        NetClient.Connect(serverAddress, port);
        SendPacketClient(ClientPackets.CreateLoginPacket_(username, auth, serverPassword));
    }

    private void Reconnect() => IsReconnecting = true;
}