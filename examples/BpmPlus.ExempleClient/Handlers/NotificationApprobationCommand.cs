using BpmPlus.Abstractions;

namespace BpmPlus.ExempleClient.Handlers;

[BpmCommande("NotificationApprobationCommand")]
public class NotificationApprobationHandler : IBpmHandlerCommande
{
    public Task ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        Console.WriteLine($"  |   [Handler] NotificationApprobation — instance #{idInstance}, commande #{aggregateId} APPROUVÉE.");
        Console.WriteLine("  |             (Envoi d'un email de confirmation au client...)");
        return Task.CompletedTask;
    }
}
