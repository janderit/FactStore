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
        private int? _threadId;

        private readonly EventStorage _storage;
        private readonly Func<object, string> _discriminatorFactory;
        private int _lastTransaction = InvalidTransaction;
        private readonly Dictionary<Guid, int> _streamversions;

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
            : this(storage, preload, notifier_target, null)
        {
        }

        public SinglethreadEventStore(EventStorage storage, IEnumerable<EventSet> preload, Action<int> notifier_target, Func<object, string> discriminator_factory)
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

        public event Action<int> CommitHook;

        private void Refresh()
        {
            var last = _storage.LastTransactionId;
            _lastTransaction = last ?? InvalidTransaction;
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
            if (_threadId == null) _threadId = Thread.CurrentThread.ManagedThreadId;
            if (_threadId.Value != Thread.CurrentThread.ManagedThreadId) throw new Exception("SinglethreadEventStore must be used from a single thread.");
            return new CollectionTransaction(Guid.NewGuid(), FindNextStreamVersion, () => DateTime.UtcNow, StartCommit, _discriminatorFactory);
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

        private int? Commit(IEnumerable<EventEnvelope> events, int transation_token)
        {
            if (_lastTransaction != transation_token) return null;
            var commit_id = _lastTransaction + 1;
            _lastTransaction = commit_id;
            foreach (var @event in events)
            {
                if (_streamversions.ContainsKey(@event.Header.Stream))
                    _streamversions[@event.Header.Stream] = @event.Header.StreamVersion;
                else _streamversions.Add(@event.Header.Stream, @event.Header.StreamVersion);
            }
            _storage.Add(commit_id, new EventSet(events, commit_id));
            _notifierTarget(commit_id);
            NotifyCommitHook(commit_id);
            return commit_id;
        }
    }
}