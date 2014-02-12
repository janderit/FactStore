using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JIT.FactStore.Internals;

namespace JIT.FactStore
{
    public sealed class SinglethreadEventStore : EventStore
    {
        private readonly Action<int> _notifierTarget;
        private const int InvalidTransaction = -1;
        private int? ThreadId;

        private readonly EventStorage _storage;
        private int _last_transaction = InvalidTransaction;


        public SinglethreadEventStore(EventStorage storage)
            : this(storage, null, null)
        {
        }

        public SinglethreadEventStore(EventStorage storage, Action<int> notifier_target)
            : this(storage, null, notifier_target)
        {
        }

        public SinglethreadEventStore(EventStorage storage, IEnumerable<EventSet> preload)
            : this(storage, preload, null)
        {
        }

        public SinglethreadEventStore(EventStorage storage, IEnumerable<EventSet> preload, Action<int> notifier_target)
        {
            _storage = storage;
            Refresh();
            _notifierTarget = notifier_target ?? (_ => { });
            Preload(preload ?? new List<EventSet>());
        }

        public event Action<int> CommitHook;

        private void Refresh()
        {
            var last = _storage.LastTransactionId;
            _last_transaction = last ?? InvalidTransaction;
        }

        private void NotifyCommitHook(int commit)
        {
            var handler = CommitHook;
            if (handler != null) handler(commit);
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
                yield return _storage.Get(i);
            }
        }

        public EventStoreTransaction StartTransaction()
        {
            if (ThreadId == null) ThreadId = Thread.CurrentThread.ManagedThreadId;
            if (ThreadId.Value != Thread.CurrentThread.ManagedThreadId) throw new Exception("SinglethreadEventStore must be used from a single thread.");
            return new CollectionTransaction(Guid.NewGuid(), FindStreamVersion, () => DateTime.UtcNow, StartCommit);
        }

        private int FindStreamVersion(Guid stream)
        {
            return _storage.All.SelectMany(es => es.Envelopes.Where(env => env.Header.Stream == stream)).OrderBy(_ => _.Header.StreamVersion).Select(_ => _.Header.StreamVersion).LastOrDefault();
        }

        private Func<IEnumerable<EventEnvelope>, int?> StartCommit()
        {
            var last = _last_transaction;
            return events => Commit(events, last);
        }

        private int? Commit(IEnumerable<EventEnvelope> events, int transation_token)
        {
            if (_last_transaction != transation_token) return null;
            var commit_id = _last_transaction + 1;
            _last_transaction = commit_id;
            _storage.Add(commit_id, new EventSet(events, commit_id));
            _notifierTarget(commit_id);
            NotifyCommitHook(commit_id);
            return commit_id;
        }
    }
}