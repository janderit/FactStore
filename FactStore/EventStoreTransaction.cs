using System;

namespace JIT.FactStore
{
    public interface EventStoreTransaction
    {
        void Store(object @event, string discriminator, Guid stream, int? version=null);
        AsyncTask<int> Commit();
    }
}