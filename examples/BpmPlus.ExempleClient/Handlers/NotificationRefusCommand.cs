using BpmPlus.Abstractions;

namespace BpmPlus.ExempleClient.Handlers;

/// <summary>
/// Handler du noeud final "notification-refus".
/// Convention : PascalCase("notification-refus") + "Command" = "NotificationRefusCommand".
/// </summary>
public class NotificationRefusCommand : IBpmHandlerCommande
{
    public string NomCommande => "NotificationRefusCommand";

    public Task ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        var montant = contexte.Variables.ObtenirOuDefaut<decimal>("montant");

        Console.WriteLine($"  |   [Handler] NotificationRefus — commande #{aggregateId} REFUSÉE (montant {montant:C}).");
        Console.WriteLine("  |             (Envoi d'un email de refus au client...)");
        return Task.CompletedTask;
    }
}
