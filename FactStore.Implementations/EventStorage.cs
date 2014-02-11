using System.Collections.Generic;

namespace JIT.FactStore
{
    public interface EventStorage
    {
        void Add(int commit, EventSet eventSet);
        EventSet Get(int commit);
        IEnumerable<EventSet> All { get; }
    }
}