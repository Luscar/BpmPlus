namespace BpmPlus.Abstractions;

/// <summary>
/// Interface de base non-générique pour la résolution des handlers query via Autofac.
/// </summary>
public interface IBpmHandlerQuery { }

/// <summary>
/// Handler d'une query BPM. Décoré avec [BpmQuery("...")] pour la découverte automatique.
/// </summary>
public interface IBpmHandlerQuery<TResultat> : IBpmHandlerQuery
{
    Task<TResultat> ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte);
}
