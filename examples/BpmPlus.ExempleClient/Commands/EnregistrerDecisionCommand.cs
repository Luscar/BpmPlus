using BpmPlus.Abstractions;

namespace BpmPlus.ExempleClient.Commands;

/// <summary>
/// Commande de la CommandePost du noeud interactif "approbation-responsable".
/// Exécutée dans la même transaction que la reprise de l'instance.
/// </summary>
public record EnregistrerDecisionCommand : IBpmCommande
{
    public string NomCommande => "EnregistrerDecisionCommand";
}
