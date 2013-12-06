
using JIT.FactStore;
using NUnit.Framework;

namespace FactStore.Specs
{ 
	[TestFixture]
    public class InMemoryEventStoreTest : EventStore_Specification {
	    protected override EventStore SubjectFactory()
	    {
	        return new SingleThreadInMemoryEventStore();
	    }
    }
}