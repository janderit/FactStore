using System;

namespace JIT.FactStore
{
    public abstract class EventStoreException : Exception
    {
        protected EventStoreException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected EventStoreException(string message) : base(message)
        {
        }
    }
}