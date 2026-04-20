namespace BpmPlus.Abstractions;

/// <summary>
/// Interface de base non-générique pour la découverte des handlers query.
/// </summary>
public interface IBpmHandlerQuery
{
    string NomQuery { get; }
}

/// <summary>
/// Handler d'une query BPM. Découvert automatiquement via NomQuery.
/// </summary>
public interface IBpmHandlerQuery<TResultat> : IBpmHandlerQuery
{
    Task<TResultat> ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte);
}
