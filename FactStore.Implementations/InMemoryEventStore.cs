using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JIT.FactStore.Internals;

namespace JIT.FactStore
{
    public sealed class InMemoryEventStore : EventStore
    {
        private readonly Action<int> _notifierTarget;
        private const int InvalidTransaction = -1;

        private readonly Dictionary<int, EventSet> _store = new Dictionary<int, EventSet>();
        private int _last_transaction = InvalidTransaction;


        public InMemoryEventStore()
            : this(null, null)
        {
        }

        public InMemoryEventStore(Action<int> notifier_target)
            : this(null, notifier_target)
        {
        }

        public InMemoryEventStore(IEnumerable<EventSet> preload)
            : this(preload, null)
        {
        }

        public InMemoryEventStore(IEnumerable<EventSet> preload, Action<int> notifier_target)
        {
            _notifierTarget = notifier_target ?? (_ => { });
            Preload(preload ?? new List<EventSet>());
        }

        private void Preload(IEnumerable<EventSet> sets)
        {
            foreach (var eventSet in sets)
            {
                Commit(eventSet.Envelopes, _last_transaction);
            }
        }


        public int LastTransaction
        {
            get { return _last_transaction; }
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
            var stop = Math.Min(upto, _last_transaction);
            for (var i = start; i <= stop; i++)
            {
                yield return _store[i];
            }
        }

        public EventStoreTransaction StartTransaction()
        {
            return new CollectionTransaction(Guid.NewGuid(), FindStreamVersion, () => DateTime.UtcNow, StartCommit);
        }

        public event Action<int> CommitHook;

        private void NotifyCommitHook(int commit)
        {
            var handler = CommitHook;
            if (handler != null) handler(commit);
        }

        private int FindStreamVersion(Guid stream)
        {
            return _store.Values.SelectMany(es => es.Envelopes.Where(env => env.Header.Stream == stream)).OrderBy(_ => _.Header.StreamVersion).Select(_ => _.Header.StreamVersion).LastOrDefault();
        }

        private Func<IEnumerable<EventEnvelope>, int?> StartCommit()
        {
            var last = _last_transaction;
            return events => Commit(events, last);
        }

        private SpinLock _lock = new SpinLock(enableThreadOwnerTracking:true);

        private int? Commit(IEnumerable<EventEnvelope> events, int transation_token)
        {
            var lock_taken = false;

            int commit_id;
            try
            {
                if (!_lock.IsHeldByCurrentThread) _lock.Enter(ref lock_taken);

                if (_last_transaction != transation_token) return null;
                commit_id = _last_transaction + 1;
                _last_transaction = commit_id;
                _store.Add(commit_id, new EventSet(events, commit_id));
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