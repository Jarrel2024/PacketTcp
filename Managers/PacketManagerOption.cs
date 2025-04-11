using PacketTcp.Cryptos;
using PacketTcp.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PacketTcp.Managers;
/// <summary>
/// PacketManagerOption is used to configure the packet manager.
/// </summary>
/// <param name="manager"></param>
public class PacketManagerOption(PacketManager manager)
{
    /// <summary>
    /// The maximum size of a packet in bytes.
    /// </summary>
    public int MaxPacketSize { get; set; } = 4096;
    /// <summary>
    /// The maximum count of packet
    /// </summary>
    public int MaxPacketCount { get; set; } = 100;
    /// <summary>
    /// The maximum count of a packet each client.
    /// </summary>
    public int MaxPacketCountPerClient { get; set; } = 100;
    /// <summary>
    /// The maximum count of client connections.
    /// </summary>
    public int MaxClientCount { get; set; } = 100;
    /// <summary>
    /// The maximum time to wait for a packet in milliseconds.
    /// </summary>
    public int PacketWaitTime { get; set; } = 10;
    /// <summary>
    /// Sync client id to clients.
    /// </summary>
    public bool SyncClientId { get => _syncClientId; 
        set 
        { 
            _syncClientId = value;
            if (SyncClientId)
            {
                manager.RegisterPacket<RequestClientIDC2SPacket>();
                manager.RegisterPacket<RequestClientIDS2CPacket>();
            }
        } 
    }
    private bool _syncClientId = false;
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
            manager.RegisterPacket(type);
        }
    }
    /// <summary>
    /// Use RSA crypto for encrypt and decrypt data.
    /// </summary>
    public RSACryptoProvider UseRSACrypto()
    {
        var provider = new RSACryptoProvider();
        CryptoProvider = provider;
        return provider;
    }
    /// <summary>
    /// Use AES crypto for encrypt and decrypt data.
    /// </summary>
    public AESCryptoProvider UseAESCrypto()
    {
        var provider = new AESCryptoProvider();
        CryptoProvider = provider;
        return provider;
    }
    internal bool UseCrypto => CryptoProvider != null;
    public ICryptoProvider? CryptoProvider { get; set; } = null;
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // 使用驼峰命名
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault, // 忽略默认值
        WriteIndented = false, // 禁用缩进
        Converters =
        {
            new System.Text.Json.Serialization.JsonStringEnumConverter() // 枚举值序列化为字符串
        }
    };
}
