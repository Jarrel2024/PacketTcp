using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PacketTcp;

/// <summary>
/// Base class for all packets.
/// </summary>
public class Packet
{
    [JsonIgnore]
    internal Guid? PakcetId { get; set; } = null;
}