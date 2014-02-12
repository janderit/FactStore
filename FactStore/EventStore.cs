using System;
using System.Collections.Generic;

namespace JIT.FactStore
{
    public interface EventStore
    {
        Int32 LastTransaction { get; }
        AsyncTask<IEnumerable<EventSet>> Commits(int startwith, int upto);
        EventStoreTransaction StartTransaction();
        event Action<int> CommitHook;

        void Refresh();
    }
}
