using BpmPlus.Abstractions;

namespace BpmPlus.ExempleClient.Handlers;

/// <summary>
/// Handler du noeud final "notification-approbation".
/// Convention : PascalCase("notification-approbation") + "Command" = "NotificationApprobationCommand".
/// </summary>
public class NotificationApprobationCommand : IBpmHandlerCommande
{
    public string NomCommande => "NotificationApprobationCommand";

    public Task ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        Console.WriteLine($"  |   [Handler] NotificationApprobation — commande #{aggregateId} APPROUVÉE.");
        Console.WriteLine("  |             (Envoi d'un email de confirmation au client...)");
        return Task.CompletedTask;
    }
}
