using System.Text.Json.Serialization;

namespace PacketTcp;

/// <summary>
/// Base class for all packets.
/// </summary>
public class Packet
{
    [JsonIgnore]
    internal Guid? PakcetId { get; set; } = null;
}