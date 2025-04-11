using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PacketTcp;
internal class BufferResolver(ref byte[] buffer)
{
    private byte[] Buffer { get; } = buffer;
    public Queue<byte[]> Resolve(int packetSize)
    {
        if (packetSize<48) return [];
        Queue<byte[]> packets = new();
        using var ms = new MemoryStream(Buffer);
        using var br = new BinaryReader(ms);
        int offset = 0;
        while (offset < packetSize)
        {
            int remaining = packetSize - offset;
            if (remaining< sizeof(int)) break;
            int size = br.ReadInt32();
            if (size < 48 || size > remaining) break;
            packets.Enqueue(br.ReadBytes(size));
            offset += size + sizeof(int);
        }
        return packets;
    }
}
