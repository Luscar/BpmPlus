namespace BpmPlus.Abstractions;

/// <summary>
/// Interface de base non-générique pour la découverte des handlers query.
/// </summary>
public interface IHandlerQuery
{
    string NomQuery { get; }
}

/// <summary>
/// Query exécutable par le moteur BPM pour prendre une décision (NoeudDecision)
/// ou résoudre une date d'échéance (NoeudAttenteTemps).
/// </summary>
public interface IHandlerQuery<TResultat> : IHandlerQuery
{
    Task<TResultat> ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte);
}
