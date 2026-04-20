using BpmPlus.Abstractions;

namespace BpmPlus.ExempleClient.Handlers;

public class EnregistrerDecisionHandler : IBpmHandlerCommande
{
    public string NomCommande => "EnregistrerDecisionCommand";

    public Task ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        var statut = contexte.Variables.ObtenirOuDefaut<string>("statut") ?? "Inconnu";

        Console.WriteLine($"  |   [Handler] EnregistrerDecision — instance #{idInstance}, commande #{aggregateId}, statut = {statut}");

        contexte.Variables.Definir("dateDecision", DateTime.UtcNow);

        return Task.CompletedTask;
    }
}
