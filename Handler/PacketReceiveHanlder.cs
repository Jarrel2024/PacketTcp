using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PacketTcp.Handler;
internal class PacketReceiveHanlder : IHandler
{
    public required Type PacketType;
    public required Delegate Handler;
}
