using System;
using System.Collections.Generic;
using System.Linq;

namespace JIT.FactStore.Internals
{
    class CollectionTransaction : EventStoreTransaction
    {
        private const int MaxRetries = 3;

        private readonly Guid _transactionId;
        private readonly Func<Guid, int> _findStreamVersion;
        private readonly Func<DateTime> _clockProvider;
        private readonly Func<Func<IEnumerable<EventEnvelope>, int?>> _beginCommit;

        private readonly List<Tuple<object, string, Guid, int?, DateTime>> _submissions = new List<Tuple<object, string, Guid, int?, DateTime>>();

        internal CollectionTransaction(Guid transaction_id, Func<Guid, int> findStreamVersion, Func<DateTime> clock_provider, Func<Func<IEnumerable<EventEnvelope>, int?>> begin_commit)
        {
            _transactionId = transaction_id;
            _findStreamVersion = findStreamVersion;        
            _clockProvider = clock_provider;
            _beginCommit = begin_commit;
        }

        public void Store(object @event, string discriminator, Guid stream, int? version)
        {
            _submissions.Add(new Tuple<object, string, Guid, int?, DateTime>(@event, discriminator, stream, version, _clockProvider()));
        }

        public AsyncTask<int> Commit()
        {
            try
            {
                for (var a = 0; a < MaxRetries; a++)
                {
                    int? commit_id;
                    try
                    {
                        var commit = _beginCommit();
                        var eventSet = _submissions.Select(Wrap);
                        commit_id = commit(eventSet);
                    }
                    catch (Exception ex)
                    {
                        throw new EventStoreCommitFailedWithErrorException(ex);
                    }
                    if (commit_id.HasValue) return on_commit => on_commit.Result(commit_id.Value);                    
                }
                throw new EventStoreCommitFailedException(MaxRetries);
            }
            catch (Exception ex)
            {
                return on_commit => on_commit.Error(ex);
            }
        }

        private EventEnvelope Wrap(Tuple<object, string, Guid, int?, DateTime> arg)
        {
            return new EventEnvelope(new EventHeader(_transactionId, arg.Item5, arg.Item2, arg.Item3, arg.Item4 ?? _findStreamVersion(arg.Item3)), arg.Item1);
        }
    }
}