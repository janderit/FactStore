using System;
using System.Collections.Generic;
using System.Linq;

namespace JIT.FactStore.Internals
{
    class CollectionTransaction : EventStoreTransaction
    {
        private const int MaxRetries = 3;

        private readonly Guid _transactionId;
        private readonly Func<Guid, int> _findNextStreamVersion;
        private readonly Func<DateTime> _clockProvider;
        private readonly Func<Func<IEnumerable<EventEnvelope>, int?>> _beginCommit;
        private readonly Func<object, string> _discriminatorFactory;

        private readonly List<Tuple<object, string, Guid, int?, DateTime>> _submissions = new List<Tuple<object, string, Guid, int?, DateTime>>();

        internal CollectionTransaction(Guid transaction_id, Func<Guid, int> findNextStreamVersion, Func<DateTime> clock_provider, Func<Func<IEnumerable<EventEnvelope>, int?>> begin_commit, Func<object, string> discriminatorFactory)
        {
            _transactionId = transaction_id;
            _findNextStreamVersion = findNextStreamVersion;        
            _clockProvider = clock_provider;
            _beginCommit = begin_commit;
            _discriminatorFactory = discriminatorFactory;
        }

        public void Store(object @event, string discriminator, Guid stream, int? version)
        {
            if (discriminator == null && _discriminatorFactory != null) discriminator = _discriminatorFactory(@event);
            _submissions.Add(new Tuple<object, string, Guid, int?, DateTime>(@event, discriminator, stream, version, _clockProvider()));
        }

        private Dictionary<Guid, int> _locally_increased_Versions = new Dictionary<Guid, int>();

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
                        var eventSet = _submissions.Select(Wrap).ToList();
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
            var lastversion = _locally_increased_Versions.ContainsKey(arg.Item3)
                ? _locally_increased_Versions[arg.Item3] + 1
                : _findNextStreamVersion(arg.Item3);

            int version;
            if (arg.Item4.HasValue)
            {
                if (arg.Item4.Value < lastversion) throw new Exception("Versionskonflikt im Stream " + arg.Item3);
                version = arg.Item4.Value;
            }
            else
            {
                version = lastversion;
            }
            if (_locally_increased_Versions.ContainsKey(arg.Item3)) _locally_increased_Versions[arg.Item3] = version; else _locally_increased_Versions.Add(arg.Item3, version);
            return new EventEnvelope(new EventHeader(_transactionId, arg.Item5, arg.Item2, arg.Item3, version), arg.Item1);
        }
    }
}