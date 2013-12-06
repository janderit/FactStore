using System;

namespace JIT.FactStore
{
    [Serializable]
    public abstract class EventStoreException : Exception
    {
        protected EventStoreException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected EventStoreException(string message) : base(message)
        {
        }
    }

    [Serializable]
    public sealed class EventStoreCommitFailedWithErrorException : EventStoreException
    {
        public EventStoreCommitFailedWithErrorException(Exception innerException) : base("Commit to event store failed with error: "+innerException.Message, innerException)
        {
        }
    }

    [Serializable]
    public sealed class EventStoreCommitFailedException : EventStoreException
    {
        public EventStoreCommitFailedException(int tries) : base("Commit to event store failed due to hard concurrency failure after "+tries+" tries.")
        {
        }
    }
}