using BpmPlus.Abstractions;

namespace BpmPlus.ExempleClient.Handlers;

public class NotificationRefusHandler : IBpmHandlerCommande
{
    public string NomCommande => "NotificationRefusCommand";

    public Task ExecuterAsync(
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
