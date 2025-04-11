using PacketTcp;
using PacketTcp.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PacketTcp.Managers;

/// <summary>
/// PacketManager is used to manage packets.
/// </summary>
public class PacketManager
{
    internal PacketManagerOption Option { get; set; } = new();
    internal Dictionary<Guid,Type> Packets { get; set; } = new();
    internal Dictionary<Type, Guid> Ids { get; set; } = new();

    /// <summary>
    /// PacketManager constructor.
    /// </summary>
    /// <param name="options">Options for server or client</param>
    public PacketManager(Action<PacketManagerOption>? options = null)
    {
        if (options == null) return;
        options.Invoke(Option);

        if (Option.SyncClientId)
        {
            this.RegisterPacket<RequestClientIDC2SPacket>();
            this.RegisterPacket<RequestClientIDS2CPacket>();
        }
    }
    /// <summary>
    /// Register a packet type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public void RegisterPacket<T>() where T : Packet
    { 
        RegisterPacket(typeof(T));
    }

    /// <summary>
    /// Register a packet type.
    /// </summary>
    /// <param name="packetType"></param>
    public void RegisterPacket(Type packetType)
    {
        Guid packetId = packetType.GUID;
        Packets.Add(packetId, packetType);
        Ids.Add(packetType, packetId);
    }

    /// <summary>
    /// Deserialize a packet from byte array.
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    /// <exception cref="InvalidCastException"></exception>
    public (Type PacketType,Packet Packet) DeserializePacket(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);
        Guid packetId = new Guid(br.ReadBytes(16));
        Guid guid = new Guid(br.ReadBytes(16));
        int length = br.ReadInt32();
        byte[] d = br.ReadBytes(length);
        object obj = JsonSerializer.Deserialize(d, Packets[packetId])??throw new InvalidCastException();
        Packet packet = (Packet)obj;
        packet.PakcetId = guid;
        return (Packets[packetId], packet);
    }

    /// <summary>
    /// Deserialize a packet from byte array.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data"></param>
    /// <returns></returns>
    public T DeserializePacket<T>(byte[] data) where T : Packet
    {
        return (T)DeserializePacket(data).Packet;
    }

    /// <summary>
    /// Serialize a packet to byte array.
    /// </summary>
    /// <param name="packet"></param>
    /// <returns></returns>
    public byte[] SerializePacket(Packet packet)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        Guid packetId = Ids[packet.GetType()];
        bw.Write(packetId.ToByteArray());
        if (packet.PakcetId == null) packet.PakcetId = Guid.NewGuid();
        bw.Write(packet.PakcetId.Value.ToByteArray());
        string json = JsonSerializer.Serialize(packet,packet.GetType());
        byte[] data = Encoding.UTF8.GetBytes(json);
        bw.Write(data.Length);
        bw.Write(data);
        return ms.ToArray();
    }

    /// <summary>
    /// Auto register all packets in the assembly.
    /// </summary>
    public void MapPackets()
    {
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.GetCustomAttribute<PacketAttribute>() != null);
        foreach (Type type in types)
        {
            RegisterPacket(type);
        }
    }
}
