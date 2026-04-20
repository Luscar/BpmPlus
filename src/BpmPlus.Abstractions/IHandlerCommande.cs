namespace BpmPlus.Abstractions;

/// <summary>
/// Handler d'une commande BPM. Découvert automatiquement via NomCommande.
/// </summary>
public interface IBpmHandlerCommande
{
    string NomCommande { get; }

    Task ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte);
}
