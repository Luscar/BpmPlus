namespace BpmPlus.Abstractions;

/// <summary>
/// Commande métier exécutable par le moteur BPM lors du traitement d'un NoeudMetier.
/// Chaque implémentation est découverte automatiquement par son NomCommande.
/// </summary>
public interface IHandlerCommande
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
