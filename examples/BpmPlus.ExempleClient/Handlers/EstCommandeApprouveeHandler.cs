using BpmPlus.Abstractions;

namespace BpmPlus.ExempleClient.Handlers;

[BpmQuery("EstCommandeApprouveeQuery")]
public class EstCommandeApprouveeHandler : IBpmHandlerQuery<bool>
{
    public Task<bool> ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        var statut = contexte.Variables.ObtenirOuDefaut<string>("statut");
        var approuvee = statut == "Approuvee";

        Console.WriteLine($"  |   [Query]   EstCommandeApprouvee — instance #{idInstance}, commande #{aggregateId}, statut = {statut} → {approuvee}");

        return Task.FromResult(approuvee);
    }
}
