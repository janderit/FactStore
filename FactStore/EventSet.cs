using System.Collections.Generic;
using System.Linq;

namespace JIT.FactStore
{
    public sealed class EventSet
    {
        public EventSet(IEnumerable<EventEnvelope> envelopes, int id)
        {
            Id = id;
            _envelopes = envelopes.ToList();
        }
        
        private readonly List<EventEnvelope> _envelopes;
        public IEnumerable<EventEnvelope> Envelopes { get { return _envelopes; } }
        public int Id;
    }
}