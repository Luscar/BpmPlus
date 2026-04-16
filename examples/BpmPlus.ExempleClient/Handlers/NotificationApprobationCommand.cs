using BpmPlus.Abstractions;
using BpmPlus.ExempleClient.Commands;

namespace BpmPlus.ExempleClient.Handlers;

/// <summary>
/// Handler du noeud final "notification-approbation".
/// </summary>
public class NotificationApprobationHandler : BpmHandlerCommande<NotificationApprobationCommand>
{
    public override Task ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        Console.WriteLine($"  |   [Handler] NotificationApprobation — commande #{aggregateId} APPROUVÉE.");
        Console.WriteLine("  |             (Envoi d'un email de confirmation au client...)");
        return Task.CompletedTask;
    }
}
