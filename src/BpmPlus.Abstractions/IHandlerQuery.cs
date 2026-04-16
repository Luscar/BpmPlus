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

/// <summary>
/// Handler typé pour une query BPM spécifique.
/// Hérite de IBpmHandlerQuery&lt;TResultat&gt; pour la compatibilité avec le moteur.
/// Le NomQuery est déduit automatiquement de TQuery.
/// </summary>
public interface IBpmHandlerQuery<TQuery, TResultat> : IBpmHandlerQuery<TResultat>
    where TQuery : IBpmQuery<TResultat>, new()
{
}

/// <summary>
/// Classe de base abstraite pour les handlers de queries typés.
/// Fournit NomQuery depuis TQuery, sans duplication dans le handler.
/// </summary>
/// <example>
/// public record EstCommandeUrgenteQuery : IBpmQuery&lt;bool&gt;
/// {
///     public string NomQuery => "EstCommandeUrgenteQuery";
/// }
///
/// public class EstCommandeUrgenteHandler : BpmHandlerQuery&lt;EstCommandeUrgenteQuery, bool&gt;
/// {
///     public override Task&lt;bool&gt; ExecuterAsync(long? aggregateId,
///         IReadOnlyDictionary&lt;string, object?&gt; parametres, IContexteExecution contexte)
///     {
///         // logique d'évaluation
///         return Task.FromResult(true);
///     }
/// }
/// </example>
public abstract class BpmHandlerQuery<TQuery, TResultat> : IBpmHandlerQuery<TQuery, TResultat>
    where TQuery : IBpmQuery<TResultat>, new()
{
    private static readonly string _nomQuery = new TQuery().NomQuery;

    public string NomQuery => _nomQuery;

    public abstract Task<TResultat> ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte);
}
