using BpmPlus.Abstractions;
using BpmPlus.ExempleClient.Commands;

namespace BpmPlus.ExempleClient.Handlers;

/// <summary>
/// Handler de la CommandePost du noeud interactif "approbation-responsable".
/// Exécuté dans la même transaction que la reprise, après que l'application
/// a mis à jour la variable "statut" via ModifierVariableAsync.
/// </summary>
public class EnregistrerDecisionHandler : BpmHandlerCommande<EnregistrerDecisionCommand>
{
    public override Task ExecuterAsync(
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
