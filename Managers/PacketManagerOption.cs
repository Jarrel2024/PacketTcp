using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PacketTcp.Managers;
/// <summary>
/// PacketManagerOption is used to configure the packet manager.
/// </summary>
public class PacketManagerOption
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
    public bool SyncClientId { get; set; } = true;
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
