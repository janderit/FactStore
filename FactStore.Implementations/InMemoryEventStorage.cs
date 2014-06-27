using System.Collections.Generic;
using System.Linq;

namespace JIT.FactStore
{
    public class InMemoryEventStorage : EventStorage
    {
        private readonly Dictionary<int, EventSet> _store = new Dictionary<int, EventSet>();
        public void Add(int commit, EventSet eventSet)
        {
            _store.Add(commit, eventSet);
        }

        public EventSet Get(int commit)
        {
            return _store[commit];
        }

        public IEnumerable<EventSet> All
        {
            get { return _store.Values; }
        }

        public int? LastTransactionId
        {
            get { return _store.Count == 0 ? default(int?) : _store.Keys.Max(); }
        }
    }
}