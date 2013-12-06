using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JIT.FactStore.Internals;

namespace JIT.FactStore
{
    public sealed class SingleThreadInMemoryEventStore : EventStore
    {
        private readonly Action<int> _notifierTarget;
        private const int InvalidTransaction = -1;
        private int? ThreadId;

        private readonly Dictionary<int, EventSet> _store = new Dictionary<int, EventSet>();
        private int _last_transaction = InvalidTransaction;


        public SingleThreadInMemoryEventStore()
            : this(null, null)
        {
        }

        public SingleThreadInMemoryEventStore(Action<int> notifier_target)
            : this(null, notifier_target)
        {
        }

        public SingleThreadInMemoryEventStore(IEnumerable<EventSet> preload)
            : this(preload, null)
        {
        }

        public SingleThreadInMemoryEventStore(IEnumerable<EventSet> preload, Action<int> notifier_target)
        {
            _notifierTarget = notifier_target ?? (_ => { });
            Preload(preload ?? new List<EventSet>());
        }

        public event Action<int> CommitHook;

        private void NotifyCommitHook(int commit)
        {
            var handler = CommitHook;
            if (handler != null) handler(commit);
        }

        private void Preload(IEnumerable<EventSet> sets)
        {
            foreach (var eventSet in sets)
            {
                Commit(eventSet, _last_transaction);
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
            if (ThreadId == null) ThreadId = Thread.CurrentThread.ManagedThreadId;
            if (ThreadId.Value != Thread.CurrentThread.ManagedThreadId) throw new Exception("SingleThreadInMemoryEventStore must be used from a single thread.");
            return new CollectionTransaction(Guid.NewGuid(), FindStreamVersion, () => DateTime.UtcNow, StartCommit);
        }

        private int FindStreamVersion(Guid stream)
        {
            return _store.Values.SelectMany(es => es.Envelopes.Where(env => env.Header.Stream == stream)).OrderBy(_ => _.Header.StreamVersion).Select(_ => _.Header.StreamVersion).LastOrDefault();
        }

        private Func<EventSet, int?> StartCommit()
        {
            var last = _last_transaction;
            return eventset => Commit(eventset, last);
        }

        private int? Commit(EventSet eventSet, int transation_token)
        {
            if (_last_transaction != transation_token) return null;
            var commit_id = _last_transaction + 1;
            _last_transaction = commit_id;
            _store.Add(commit_id, eventSet);
            _notifierTarget(commit_id);
            NotifyCommitHook(commit_id);
            return commit_id;
        }
    }
}