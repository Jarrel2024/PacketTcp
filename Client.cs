using System.Net.Sockets;

namespace PacketTcp;
public class Client
{
    public required Socket Socket { get; set; }
    public required Guid Id { get; set; }
    public bool IsConnected => Socket.Connected;

    internal PacketServer? PacketServer { get; set; }
    internal PacketClient? PacketClient { get; set; }

    public void Send(Packet packet)
    {
        PacketServer?.Send(Id, packet);
        PacketClient?.Send(packet);
    }
}
