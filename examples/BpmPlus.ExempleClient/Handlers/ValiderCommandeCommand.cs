using BpmPlus.Abstractions;
using BpmPlus.ExempleClient.Commands;

namespace BpmPlus.ExempleClient.Handlers;

/// <summary>
/// Handler du noeud "valider-commande".
/// BpmHandlerCommande&lt;T&gt; fournit NomCommande depuis ValiderCommandeCommand automatiquement.
/// </summary>
public class ValiderCommandeHandler : BpmHandlerCommande<ValiderCommandeCommand>
{
    public override Task ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        var montant = contexte.Variables.ObtenirOuDefaut<decimal>("montant");

        Console.WriteLine($"  |   [Handler] ValiderCommande  — commande #{aggregateId}, montant {montant:C}");

        contexte.Variables.Definir("statut", "EnAttente");
        Console.WriteLine("  |   [Handler] Statut initialisé : EnAttente");

        return Task.CompletedTask;
    }
}
