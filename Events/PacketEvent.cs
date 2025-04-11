using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PacketTcp.Events;
public class PacketEvent(Type type,Packet packet,Socket client) : ICancellable
{
    internal PacketServer? server { get; set; }
    public bool IsCancelled { get; set; } = false;
    public bool FromClient { get; set; } = false;
    public Type Type { get; init; } = type;
    public Packet Packet { get; init; } = packet;
    public Socket Client { get; set; } = client;

    /// <summary>
    /// Send a callback packet to client.
    /// Only server use.
    /// </summary>
    /// <param name="packet"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void SendCallback(Packet packet)
    {
        if (server == null) throw new InvalidOperationException("Server is not set.");
        packet.PakcetId = Packet.PakcetId;
        server.Send(Client, packet);
    }
}
