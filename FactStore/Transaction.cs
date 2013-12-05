using System;

namespace JIT.FactStore
{
    public interface Transaction
    {
        void Store(object @event, string discriminator, Guid stream);
        void Commit(Action<int> on_stored, Action<Exception> on_error);
    }
}