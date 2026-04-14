using System.Data;
using BpmPlus.Abstractions;

namespace BpmPlus.Core.Execution;

public class ContexteExecution : IContexteExecution
{
    public long IdInstance { get; }
    public string CleDefinition { get; }
    public int VersionDefinition { get; }
    public long? AggregateId { get; }
    public IDbTransaction Transaction { get; }
    public IAccesseurVariables Variables { get; }
    public CancellationToken CancellationToken { get; }

    public ContexteExecution(
        long idInstance,
        string cleDefinition,
        int versionDefinition,
        long? aggregateId,
        IDbTransaction transaction,
        IAccesseurVariables variables,
        CancellationToken cancellationToken)
    {
        IdInstance = idInstance;
        CleDefinition = cleDefinition;
        VersionDefinition = versionDefinition;
        AggregateId = aggregateId;
        Transaction = transaction;
        Variables = variables;
        CancellationToken = cancellationToken;
    }
}
