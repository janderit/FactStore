using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JIT.FactStore
{
    public interface EventStore
    {
        Int32 LastTransaction { get; }

        EventSet Transaction(int id);
        IEnumerable<EventSet> Transactions(int startwith, int upto);

        Transaction StartTransaction();
    }
}
