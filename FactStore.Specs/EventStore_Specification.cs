﻿using System;
using System.Linq;
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
        public void Storing_an_event_with_outdated_version_fails_1()
        {
            var subject = SubjectFactory();

            var old_last_id = subject.LastTransaction;

            var transaction = subject.StartTransaction();
            var stream = Guid.NewGuid();
            transaction.Store(new object(), "", stream, 0);
            transaction.Store(new object(), "", stream, 1);
            transaction.Store(new object(), "", stream, 0);
            Action action = ()=>transaction.Commit().Await();
            action.ShouldThrow<Exception>();

            subject.LastTransaction.Should().Be(old_last_id);
        }

        [Test]
        public void Storing_an_event_with_outdated_version_fails_2()
        {
            var subject = SubjectFactory();

            var transaction = subject.StartTransaction();
            var stream = Guid.NewGuid();
            transaction.Store(new object(), "", stream, 0);
            transaction.Store(new object(), "", stream, 1);
            transaction.Commit().Await();

            var old_last_id = subject.LastTransaction;

            transaction = subject.StartTransaction();
            transaction.Store(new object(), "", stream, 0);
            Action action = () => transaction.Commit().Await();
            action.ShouldThrow<Exception>();

            subject.LastTransaction.Should().Be(old_last_id);
        }

        [Test]
        public void Multiple_default_events_are_assigned_correct_version_numbers()
        {
            var subject = SubjectFactory();

            var old_last_id = subject.LastTransaction;

            var transaction = subject.StartTransaction();
            var stream = Guid.NewGuid();
            transaction.Store(new object(), "", stream);
            transaction.Store(new object(), "", stream);
            transaction.Store(new object(), "", stream);
            transaction.Commit().Await();
            subject.LastTransaction.Should().BeGreaterThan(old_last_id);
            var events = subject.Commits(Int32.MinValue, Int32.MaxValue).Await().SelectMany(_ => _.Envelopes).ToList();
            events.Count(_ => _.Header.StreamVersion == -1).Should().Be(0);
            events.Count(_ => _.Header.StreamVersion == 0).Should().Be(1);
            events.Count(_ => _.Header.StreamVersion == 1).Should().Be(1);
            events.Count(_ => _.Header.StreamVersion == 2).Should().Be(1);
            events.Count(_ => _.Header.StreamVersion == 3).Should().Be(0);
        }

        [Test]
        public void Storing_an_event_fires_commit_hook()
        {
            var subject = SubjectFactory();

            int? received = null;
            Action<int> subscriber = commit => received = commit;

            subject.CommitHook += subscriber;

            var old_last_id = subject.LastTransaction;

            var transaction = subject.StartTransaction();
            transaction.Store(new object(), "", Guid.NewGuid());

            transaction.Commit().Await();

            subject.LastTransaction.Should().BeGreaterThan(old_last_id);
            received.Should().HaveValue();
            received.Value.Should().Be(subject.LastTransaction);
        }

        [Test]
        public void Commit_hook_can_be_unsubscribed_from()
        {
            
            var subject = SubjectFactory();

            int? received = null;
            Action<int> subscriber = commit => received = commit;
            subject.CommitHook += subscriber;

            var old_last_id = subject.LastTransaction;

            var transaction = subject.StartTransaction();
            transaction.Store(new object(), "", Guid.NewGuid());

            subject.CommitHook -= subscriber;
            transaction.Commit().Await();

            subject.LastTransaction.Should().BeGreaterThan(old_last_id);
            received.Should().NotHaveValue();
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


