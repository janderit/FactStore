
using JIT.FactStore;
using NUnit.Framework;

namespace FactStore.Specs
{ 
	[TestFixture]
    public class SingleThreadInMemoryEventStoreTest : EventStore_Specification
    {
	    protected override EventStore SubjectFactory()
	    {
	        return new SinglethreadEventStore(new InMemoryEventStorage());
	    }
    }

    [TestFixture]
    public class InMemoryEventStoreTest : EventStore_Specification
    {
        protected override EventStore SubjectFactory()
        {
            return new ConcurrentEventStore(new InMemoryEventStorage());
        }
    }
}