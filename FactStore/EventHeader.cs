using System;

namespace JIT.FactStore
{
    public struct EventHeader
    {
        public EventHeader(Guid transaction, DateTime timestamp, string discriminator, Guid stream, int streamVersion)
        {
            Transaction = transaction;
            Timestamp = timestamp;
            Discriminator = discriminator;
            Stream = stream;
            StreamVersion = streamVersion;
        }

        public readonly Guid Transaction;
        public readonly DateTime Timestamp;
        public readonly string Discriminator;
        public readonly Guid Stream;
        public readonly int StreamVersion;
    }
}