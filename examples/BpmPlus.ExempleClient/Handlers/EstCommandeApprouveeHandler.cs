using BpmPlus.Abstractions;
using BpmPlus.ExempleClient.Queries;

namespace BpmPlus.ExempleClient.Handlers;

/// <summary>
/// Handler de la query "EstCommandeApprouveeQuery".
/// Utilisé dans le noeud de décision "decision-approbation" via SiQuery.
/// BpmHandlerQuery&lt;T, TResultat&gt; fournit NomQuery depuis EstCommandeApprouveeQuery automatiquement.
/// </summary>
public class EstCommandeApprouveeHandler : BpmHandlerQuery<EstCommandeApprouveeQuery, bool>
{
    public override Task<bool> ExecuterAsync(
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
