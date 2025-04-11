using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PacketTcp;

/// <summary>
/// PacketAttribute is used to mark a class as a packet.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class PacketAttribute : Attribute;
/// <summary>
/// BasePacketAttribute is used to mark a class as a base packet.
/// </summary>
[AttributeUsage(AttributeTargets.Class,Inherited = true)]
public class BasePacketAttribute : PacketAttribute;