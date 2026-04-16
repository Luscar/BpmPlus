using BpmPlus.Abstractions;

namespace BpmPlus.Core.Execution;

public class ContexteExecution : IContexteExecution
{
    public long IdInstance { get; }
    public string CleDefinition { get; }
    public int VersionDefinition { get; }
    public long? AggregateId { get; }
    public IAccesseurVariables Variables { get; }
    public CancellationToken CancellationToken { get; }

    public ContexteExecution(
        long idInstance,
        string cleDefinition,
        int versionDefinition,
        long? aggregateId,
        IAccesseurVariables variables,
        CancellationToken cancellationToken)
    {
        IdInstance = idInstance;
        CleDefinition = cleDefinition;
        VersionDefinition = versionDefinition;
        AggregateId = aggregateId;
        Variables = variables;
        CancellationToken = cancellationToken;
    }
}
