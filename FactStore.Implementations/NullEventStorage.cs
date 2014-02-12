using System.Collections.Generic;

namespace JIT.FactStore
{
    /// <summary>
    /// Missing event storage. Delivers empty "All" but throws on adding...
    /// </summary>
    public sealed class NullEventStorage : EventStorage
    {
        private NullEventStorage()
        {
        }

        public static readonly NullEventStorage Singleton = new NullEventStorage();

        public void Add(int commit, EventSet eventSet)
        {
            throw new System.NotImplementedException("No event storage connected");
        }

        public EventSet Get(int commit)
        {
            throw new System.NotImplementedException("No event storage connected");
        }

        public IEnumerable<EventSet> All
        {
            get { yield break; }
        }

        public int? LastTransactionId
        {
            get { return default(int?); }
        }
    }
}