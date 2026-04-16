using BpmPlus.Abstractions;

namespace BpmPlus.ExempleClient.Queries;

/// <summary>
/// Query utilisée dans le noeud de décision "decision-approbation".
/// Retourne true si la variable "statut" vaut "Approuvee".
/// La logique est dans EstCommandeApprouveeHandler.
/// </summary>
public record EstCommandeApprouveeQuery : IBpmQuery<bool>
{
    public string NomQuery => "EstCommandeApprouveeQuery";
}
