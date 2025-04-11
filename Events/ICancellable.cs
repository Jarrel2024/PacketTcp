using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PacketTcp.Events;
public interface ICancellable
{
    public bool IsCancelled { get; set; }
    public void Cancel() => this.IsCancelled = true;
}
