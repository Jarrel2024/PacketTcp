using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PacketTcp.Packets;
internal class RequestClientIDS2CPacket : Packet
{
    public Guid ClientId { get; set; }
}
