namespace JIT.FactStore
{
    public struct EventEnvelope
    {
        public EventEnvelope(EventHeader header, object @event)
        {
            Header = header;
            Event = @event;
        }

        public readonly EventHeader Header;
        public readonly object Event;
    }
}