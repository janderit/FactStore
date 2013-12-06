using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentAssertions;
using JIT.FactStore;
using NUnit.Framework;

namespace FactStore.Specs
{
    public abstract class EventStore_Specification
    {
        protected abstract EventStore SubjectFactory();

        [Test]
        public void It_is_initially_empty()
        {
            var subject = SubjectFactory();
            subject.LastTransaction.Should().BeNegative();            
        }

        [Test]
        public void An_empty_EventStore_returns_no_EventSets()
        {
            var subject = SubjectFactory();
            subject.Commits(int.MinValue, int.MaxValue).Await().Should().BeEmpty();
        }

        [Test]
        public void Writing_to_a_transaction_does_not_store_without_commit()
        {
            var subject = SubjectFactory();
            var old_last_id = subject.LastTransaction;

            var transaction = subject.StartTransaction();
            transaction.Should().NotBeNull();
            transaction.Store(new object(),"",  Guid.NewGuid());

            subject.LastTransaction.Should().Be(old_last_id);
        }

        [Test]
        public void Storing_an_event_succeeds()
        {
            var subject = SubjectFactory();

            var old_last_id = subject.LastTransaction;

            var transaction = subject.StartTransaction();
            transaction.Store(new object(), "", Guid.NewGuid());

            transaction.Commit().Await();

            subject.LastTransaction.Should().BeGreaterThan(old_last_id);
        }

        [Test]
        public void Stored_events_can_be_retrieved()
        {
            var subject = SubjectFactory();

            var old_last_id = subject.LastTransaction;

            var id = Guid.NewGuid();
            var stream = Guid.NewGuid();

            var transaction = subject.StartTransaction();
            transaction.Store(new TestEvent(id), "disco", stream);

            var ttime = DateTime.UtcNow;
            transaction.Commit().Await();

            var sets = subject.Commits(old_last_id, subject.LastTransaction).Await().ToList();
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
