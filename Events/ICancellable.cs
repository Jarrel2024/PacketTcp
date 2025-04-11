namespace PacketTcp.Events;
public interface ICancellable
{
    public bool IsCancelled { get; set; }
    public void Cancel() => this.IsCancelled = true;
}
