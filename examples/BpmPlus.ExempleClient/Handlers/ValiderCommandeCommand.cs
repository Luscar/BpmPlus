using BpmPlus.Abstractions;

namespace BpmPlus.ExempleClient.Handlers;

/// <summary>
/// Handler du noeud "valider-commande".
/// Convention de nommage : PascalCase("valider-commande") + "Command" = "ValiderCommandeCommand".
/// Aucun appel à .CommandeNommee() n'est nécessaire dans la définition.
/// </summary>
public class ValiderCommandeCommand : IBpmHandlerCommande
{
    public string NomCommande => "ValiderCommandeCommand";

    public Task ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        var montant = contexte.Variables.ObtenirOuDefaut<decimal>("montant");

        Console.WriteLine($"  |   [Handler] ValiderCommande  — commande #{aggregateId}, montant {montant:C}");

        // Logique métier : dans une vraie application, on vérifierait
        // les règles de validation (stock, crédit, limite...) via IDbConnection.
        contexte.Variables.Definir("statut", "EnAttente");
        Console.WriteLine("  |   [Handler] Statut initialisé : EnAttente");

        return Task.CompletedTask;
    }
}
