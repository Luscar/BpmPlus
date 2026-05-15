namespace BpmPlus.Abstractions;

/// <summary>
/// Handler d'une commande BPM. Décoré avec [BpmCommande("...")] pour la découverte automatique.
/// </summary>
public interface IBpmHandlerCommande
{
    Task ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte);
}
