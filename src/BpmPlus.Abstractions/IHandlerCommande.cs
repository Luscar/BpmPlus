namespace BpmPlus.Abstractions;

/// <summary>
/// Commande métier exécutable par le moteur BPM lors du traitement d'un NoeudMetier.
/// Chaque implémentation est découverte automatiquement par son NomCommande.
/// Le préfixe Bpm permet de distinguer cette interface des éventuelles interfaces
/// IHandlerCommande déjà présentes dans l'application cliente (CQRS, etc.).
/// </summary>
public interface IBpmHandlerCommande
{
    /// <summary>
    /// Identifiant unique de la commande. Doit correspondre au NomCommande
    /// défini dans le NoeudMetier ou la CommandePre/Post d'un NoeudInteractif.
    /// </summary>
    string NomCommande { get; }

    Task ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte);
}

/// <summary>
/// Handler typé pour une commande BPM spécifique.
/// Hérite de IBpmHandlerCommande pour la compatibilité avec le moteur.
/// Le NomCommande est déduit automatiquement de TCommande.
/// </summary>
public interface IBpmHandlerCommande<TCommande> : IBpmHandlerCommande
    where TCommande : IBpmCommande, new()
{
}

/// <summary>
/// Classe de base abstraite pour les handlers de commandes typés.
/// Fournit NomCommande depuis TCommande, sans duplication dans le handler.
/// </summary>
/// <example>
/// public record ValiderCommandeCommand : IBpmCommande
/// {
///     public string NomCommande => "ValiderCommandeCommand";
/// }
///
/// public class ValiderCommandeHandler : BpmHandlerCommande&lt;ValiderCommandeCommand&gt;
/// {
///     public override Task ExecuterAsync(long? aggregateId,
///         IReadOnlyDictionary&lt;string, object?&gt; parametres, IContexteExecution contexte)
///     {
///         // logique métier
///         return Task.CompletedTask;
///     }
/// }
/// </example>
public abstract class BpmHandlerCommande<TCommande> : IBpmHandlerCommande<TCommande>
    where TCommande : IBpmCommande, new()
{
    private static readonly string _nomCommande = new TCommande().NomCommande;

    public string NomCommande => _nomCommande;

    public abstract Task ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte);
}
