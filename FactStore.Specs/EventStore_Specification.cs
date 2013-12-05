using System;
using System.Linq;
using System.Threading;
using FluentAssertions;
using JIT.FactStore;
using NUnit.Framework;

namespace FactStore.Specs
{
    public abstract class EventStore_Specification
    {
        private readonly Func<EventStore> _subjectFactory;

        protected EventStore_Specification(Func<EventStore> subject_factory)
        {
            _subjectFactory = subject_factory;
        }

        private static void WaitFor(Transaction transaction, TimeSpan timeout)
        {
            var wait = new ManualResetEventSlim();
            Exception error = null;
// ReSharper disable ImplicitlyCapturedClosure
            transaction.Commit( new_id => wait.Set(), ex => { error = ex; wait.Set(); } );
// ReSharper restore ImplicitlyCapturedClosure
            if (!wait.Wait(timeout)) throw new TimeoutException("The commit did not succeed in time");
            if (error!=null) throw new Exception("Unexpected error while committing the transaction", error);
        }


        [Test]
        public void It_is_initially_empty()
        {
            var subject = _subjectFactory();
            subject.LastTransaction.Should().BeNegative();            
        }

        [Test]
        public void An_empty_EventStore_returns_no_EventSets()
        {
            var subject = _subjectFactory();
            subject.Transaction(subject.LastTransaction).Envelopes.Should().BeEmpty();
            subject.Transactions(int.MinValue, int.MaxValue).Should().BeEmpty();
        }

        [Test]
        public void Writing_to_a_transaction_does_not_store_without_commit()
        {
            var subject = _subjectFactory();
            var old_last_id = subject.LastTransaction;

            var transaction = subject.StartTransaction();
            transaction.Should().NotBeNull();
            transaction.Store(new object(),"",  Guid.NewGuid());

            subject.LastTransaction.Should().Be(old_last_id);
        }

        [Test]
        public void Storing_an_event_succeeds()
        {
            var subject = _subjectFactory();

            var old_last_id = subject.LastTransaction;

            var transaction = subject.StartTransaction();
            transaction.Store(new object(), "", Guid.NewGuid());

            WaitFor(transaction, TimeSpan.FromMilliseconds(100));

            subject.LastTransaction.Should().NotBe(old_last_id);
        }

        [Test]
        public void Stored_events_can_be_retrieved()
        {
            var subject = _subjectFactory();

            var old_last_id = subject.LastTransaction;

            var id = Guid.NewGuid();
            var stream = Guid.NewGuid();

            var transaction = subject.StartTransaction();
            transaction.Store(new TestEvent(id), "disco", stream);

            var ttime = DateTime.UtcNow;
            WaitFor(transaction, TimeSpan.FromMilliseconds(100));

            var sets = subject.Transactions(old_last_id, subject.LastTransaction).ToList();
            sets.Should().HaveCount(1);
            var eventset = sets.Single();
            eventset.Envelopes.Should().HaveCount(1);
            var envelope = eventset.Envelopes.Single();

            envelope.Header.Should().NotBeNull();
            envelope.Header.Stream.Should().Be(stream);
            envelope.Header.StreamVersion.Should().Be(0);
            envelope.Header.Transaction.Should().Should().NotBeSameAs(Guid.Empty);
            envelope.Header.Discriminator.Should().Be("disco");
            envelope.Header.Timestamp.Should().BeCloseTo(ttime);

            envelope.Event.Should().NotBeNull();
            envelope.Event.Should().BeOfType<TestEvent>();
            ((TestEvent) envelope.Event).Id.Should().Be(id);
        }

    }
}
