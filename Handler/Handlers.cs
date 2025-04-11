using PacketTcp.Events;

namespace PacketTcp.Handler;
/// <summary>
/// PacketHandler is used to handle a packet.
/// </summary>
/// <param name="packet"></param>
public delegate void PacketHandler(PacketEvent packet);
/// <summary>
/// ClientHandler is used to handle a client.
/// </summary>
/// <param name="client"></param>
public delegate void ClientHandler(Client client);