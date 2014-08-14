using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JIT.FactStore.Internals;

namespace JIT.FactStore
{
    public sealed class ConcurrentEventStore : EventStore
    {
        private readonly Action<int> _notifierTarget;
        private const int InvalidTransaction = -1;

        private EventStorage _storage;
        private int _lastTransaction = InvalidTransaction;


        public ConcurrentEventStore(EventStorage storage)
            : this(storage, null, null)
        {
        }

        public ConcurrentEventStore(EventStorage storage, Action<int> notifier_target)
            : this(storage, null, notifier_target)
        {
        }

        public ConcurrentEventStore(EventStorage storage, IEnumerable<EventSet> preload)
            : this(storage, preload, null)
        {
        }

        public ConcurrentEventStore(EventStorage storage, IEnumerable<EventSet> preload, Action<int> notifier_target)
            : this(storage, preload, notifier_target, null)
        {
        }

        public ConcurrentEventStore(EventStorage storage, IEnumerable<EventSet> preload, Action<int> notifier_target, Func<object, string> discriminator_factory)
        {
            _storage = storage;
            _discriminatorFactory = discriminator_factory;
            Refresh();
            _notifierTarget = notifier_target ?? (_ => { });

            _streamversions =
                storage.All.SelectMany(_ => _.Envelopes)
                    .GroupBy(_ => _.Header.Stream)
                    .Select(_ => new {Stream = _.Key, Version = _.Max(a => a.Header.StreamVersion)})
                    .ToDictionary(_ => _.Stream, _ => _.Version);

            Preload(preload ?? new List<EventSet>());
        }

        private void Refresh()
        {
            var lock_taken = false;
            try
            {
                if (!_lock.IsHeldByCurrentThread) _lock.Enter(ref lock_taken);

                var last = _storage.LastTransactionId;
                _lastTransaction = last ?? InvalidTransaction;
            }
            finally
            {
                if (lock_taken) _lock.Exit(true);
            }
        }

        public EventStorage SwitchStorage(EventStorage new_storage)
        {
            var lock_taken = false;
            try
            {
                if (!_lock.IsHeldByCurrentThread) _lock.Enter(ref lock_taken);

                var old = _storage;
                _storage = new_storage;
                Refresh();
                return old;
            }
            finally
            {
                if (lock_taken) _lock.Exit(true);
            }
        }

        private void Preload(IEnumerable<EventSet> sets)
        {
            foreach (var eventSet in sets)
            {
                Commit(eventSet.Envelopes, _lastTransaction);
            }
        }


        public int LastTransaction
        {
            get { return _lastTransaction; }
        }

        public AsyncTask<IEnumerable<EventSet>> Commits(int startwith, int upto)
        {
            return on_result =>
                       {
                           try
                           {
                               on_result.Result(Retrieve(startwith, upto));
                           }
                           catch (Exception ex)
                           {
                               on_result.Error(ex);
                           }
                       };
        }

        private IEnumerable<EventSet> Retrieve(int startwith, int upto)
        {
            var start = Math.Max(startwith, 0);
            var stop = Math.Min(upto, _lastTransaction);
            for (var i = start; i <= stop; i++)
            {
                yield return _storage.Get(i);
            }
        }

        public EventStoreTransaction StartTransaction()
        {
            return new CollectionTransaction(Guid.NewGuid(), FindNextStreamVersion, () => DateTime.UtcNow, StartCommit, _discriminatorFactory);
        }

        public event Action<int> CommitHook;

        private void NotifyCommitHook(int commit)
        {
            var handler = CommitHook;
            if (handler != null) handler(commit);
        }

        private int FindNextStreamVersion(Guid stream)
        {
            return _streamversions.ContainsKey(stream) ? _streamversions[stream] + 1 : 0;
        }   

        private Func<IEnumerable<EventEnvelope>, int?> StartCommit()
        {
            var last = _lastTransaction;
            return events => Commit(events, last);
        }

        private SpinLock _lock = new SpinLock(enableThreadOwnerTracking:true);
        private readonly Func<object, string> _discriminatorFactory;
        private readonly Dictionary<Guid, int> _streamversions;

        private int? Commit(IEnumerable<EventEnvelope> events, int transation_token)
        {
            var lock_taken = false;
            int commit_id;
            try
            {
                if (!_lock.IsHeldByCurrentThread) _lock.Enter(ref lock_taken);

            if (_lastTransaction != transation_token) return null;
            commit_id = _lastTransaction + 1;
            _lastTransaction = commit_id;
            foreach (var @event in events)
            {
                if (_streamversions.ContainsKey(@event.Header.Stream))
                    _streamversions[@event.Header.Stream] = @event.Header.StreamVersion;
                else _streamversions.Add(@event.Header.Stream, @event.Header.StreamVersion);
            }
            _storage.Add(commit_id, new EventSet(events, commit_id));

            }
            finally
            {
                if (lock_taken) _lock.Exit(true);
            }

            _notifierTarget(commit_id);
            NotifyCommitHook(commit_id);
            return commit_id;
        }
    }
}