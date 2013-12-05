using System;

namespace FactStore.Specs
{
    public struct TestEvent
    {
        public TestEvent(Guid id)
        {
            Id = id;
        }

        public readonly Guid Id;
    }
}