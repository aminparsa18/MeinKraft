
using MemoryPack;
using static MeinKraft.ServerPacketService;

namespace MeinKraft;

public interface IServerPacketService
{
    void SendMessage(int clientid, string message, MessageType color);
    void SendMessage(int clientid, string message);
    void SendPacket(int clientid, Packet_Server packet);
    void SendPacket(int clientid, byte[] packet);
    int StatTotalPackets { get; set; }
    int StatTotalPacketsLength { get; set; }
}

public class ServerPacketService : IServerPacketService
{
    private readonly IClientRegistry _serverClientService;

    public ServerPacketService(IClientRegistry serverClientService)
    {
        _serverClientService = serverClientService;
    }

    public void SendMessage(int clientid, string message, MessageType color) => SendMessage(clientid, MessageTypeToString(color) + message);

    public void SendMessage(int clientid, string message)
    {
        if (clientid == GameConstants.ServerConsoleId)
        {
            ServerConsole.Receive(message);
            return;
        }

        SendPacket(clientid, ServerPackets.Message(message));
    }

    public enum MessageType { Normal, Important, Help, OpUsername, Success, Error, Admin, White, Red, Green, Yellow }
    public static string MessageTypeToString(MessageType type)
    {
        return type switch
        {
            MessageType.Normal or MessageType.White => colorNormal,
            MessageType.Important => colorImportant,
            MessageType.Help or MessageType.Red => colorHelp,
            MessageType.OpUsername or MessageType.Green => colorOpUsername,
            MessageType.Error => colorError,
            MessageType.Success => colorSuccess,
            MessageType.Admin or MessageType.Yellow => colorAdmin,
            _ => colorNormal,
        };
    }

    public static string colorNormal = "&f"; //white
    public static string colorHelp = "&4"; //red
    public static string colorOpUsername = "&2"; //green
    public static string colorSuccess = "&2"; //green
    public static string colorError = "&4"; //red
    public static string colorImportant = "&4"; // red
    public static string colorAdmin = "&e"; //yellow

    public int StatTotalPackets { get; set; }
    public int StatTotalPacketsLength { get; set; }

    public void SendPacket(int clientid, Packet_Server packet) => SendPacket(clientid, MemoryPackSerializer.Serialize(packet));

    public void SendPacket(int clientid, byte[] packet)
    {
        if (_serverClientService.Clients[clientid].IsBot)
        {
            return;
        }

        StatTotalPackets++;
        StatTotalPacketsLength += packet.Length;
        _serverClientService.Clients[clientid].Socket.SendMessage(packet.AsMemory(), MyNetDeliveryMethod.ReliableOrdered);
    }
}
