using BpmPlus.Abstractions;
using BpmPlus.ExempleClient.Commands;

namespace BpmPlus.ExempleClient.Handlers;

/// <summary>
/// Handler du noeud final "notification-refus".
/// </summary>
public class NotificationRefusHandler : BpmHandlerCommande<NotificationRefusCommand>
{
    public override Task ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        var montant = contexte.Variables.ObtenirOuDefaut<decimal>("montant");

        Console.WriteLine($"  |   [Handler] NotificationRefus — instance #{idInstance}, commande #{aggregateId} REFUSÉE (montant {montant:C}).");
        Console.WriteLine("  |             (Envoi d'un email de refus au client...)");
        return Task.CompletedTask;
    }
}
