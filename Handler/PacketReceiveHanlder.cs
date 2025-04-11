namespace PacketTcp.Handler;
internal class PacketReceiveHanlder : IHandler
{
    public required Type PacketType;
    public required Delegate Handler;
}
