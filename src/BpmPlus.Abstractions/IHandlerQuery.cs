namespace BpmPlus.Abstractions;

/// <summary>
/// Interface de base non-générique pour la découverte des handlers query.
/// Le préfixe Bpm permet de distinguer cette interface des éventuelles interfaces
/// IHandlerQuery déjà présentes dans l'application cliente (CQRS, etc.).
/// </summary>
public interface IBpmHandlerQuery
{
    string NomQuery { get; }
}

/// <summary>
/// Query exécutable par le moteur BPM pour prendre une décision (NoeudDecision)
/// ou résoudre une date d'échéance (NoeudAttenteTemps).
/// </summary>
public interface IBpmHandlerQuery<TResultat> : IBpmHandlerQuery
{
    Task<TResultat> ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte);
}
