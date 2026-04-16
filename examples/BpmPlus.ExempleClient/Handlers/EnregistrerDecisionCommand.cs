using BpmPlus.Abstractions;

namespace BpmPlus.ExempleClient.Handlers;

/// <summary>
/// CommandePost du noeud interactif "approbation-responsable".
/// Exécutée par le moteur dans la même transaction que la reprise de l'instance,
/// après que l'application a mis à jour la variable "statut" via ModifierVariableAsync.
/// </summary>
public class EnregistrerDecisionCommand : IBpmHandlerCommande
{
    public string NomCommande => "EnregistrerDecisionCommand";

    public Task ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        var statut = contexte.Variables.ObtenirOuDefaut<string>("statut") ?? "Inconnu";

        Console.WriteLine($"  |   [Handler] EnregistrerDecision — commande #{aggregateId}, statut = {statut}");

        // Dans une vraie application, on pourrait ici :
        // - Mettre à jour l'agrégat en base (via IDbConnection injecté)
        // - Créer un événement de domaine
        // - Écrire une entrée d'audit métier
        contexte.Variables.Definir("dateDecision", DateTime.UtcNow);

        return Task.CompletedTask;
    }
}
