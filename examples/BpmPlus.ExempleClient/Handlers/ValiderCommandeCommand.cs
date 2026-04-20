using BpmPlus.Abstractions;

namespace BpmPlus.ExempleClient.Handlers;

public class ValiderCommandeHandler : IBpmHandlerCommande
{
    public string NomCommande => "ValiderCommandeCommand";

    public Task ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        var montant = contexte.Variables.ObtenirOuDefaut<decimal>("montant");

        Console.WriteLine($"  |   [Handler] ValiderCommande  — instance #{idInstance}, commande #{aggregateId}, montant {montant:C}");

        contexte.Variables.Definir("statut", "EnAttente");
        Console.WriteLine("  |   [Handler] Statut initialisé : EnAttente");

        return Task.CompletedTask;
    }
}
