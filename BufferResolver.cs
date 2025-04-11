namespace PacketTcp;
internal class BufferResolver(ref byte[] buffer)
{
    private byte[] Buffer { get; } = buffer;
    private byte[] _unsolvedBuffer = Array.Empty<byte>();
    public Queue<byte[]> Resolve(int packetSize)
    {
        if (packetSize<48) return [];
        Queue<byte[]> packets = new();

        Span<byte> bytes = new(new byte[Buffer.Length+_unsolvedBuffer.Length]);
        _unsolvedBuffer.AsSpan().CopyTo(bytes);
        Buffer.AsSpan(0, packetSize).CopyTo(bytes.Slice(_unsolvedBuffer.Length));

        using var ms = new MemoryStream(bytes.ToArray());
        using var br = new BinaryReader(ms);
        int offset = 0;
        while (offset < packetSize+_unsolvedBuffer.Length)
        {
            int remaining = packetSize - offset;
            if (remaining< sizeof(int)) break;
            int size = br.ReadInt32();
            if (size < 48 || size > remaining) break;
            packets.Enqueue(br.ReadBytes(size));
            offset += size + sizeof(int);
        }
        if (offset < packetSize + _unsolvedBuffer.Length)
        {
            _unsolvedBuffer = Buffer.Skip(offset).Take(packetSize - offset).ToArray();
        }
        else
        {
            _unsolvedBuffer = Array.Empty<byte>();
        }
        return packets;
    }
}
