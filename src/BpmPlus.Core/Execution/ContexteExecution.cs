using System.Data;
using BpmPlus.Abstractions;

namespace BpmPlus.Core.Execution;

public class ContexteExecution : IContexteExecution
{
    public long IdInstance { get; }
    public string CleDefinition { get; }
    public int VersionDefinition { get; }
    public long? AggregateId { get; }
    public IDbSession Session { get; }
    public IDbTransaction? Transaction => Session.Transaction;
    public IAccesseurVariables Variables { get; }
    public CancellationToken CancellationToken { get; }

    public ContexteExecution(
        long idInstance,
        string cleDefinition,
        int versionDefinition,
        long? aggregateId,
        IDbSession session,
        IAccesseurVariables variables,
        CancellationToken cancellationToken)
    {
        IdInstance = idInstance;
        CleDefinition = cleDefinition;
        VersionDefinition = versionDefinition;
        AggregateId = aggregateId;
        Session = session;
        Variables = variables;
        CancellationToken = cancellationToken;
    }
}
