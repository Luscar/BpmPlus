using BpmPlus.Abstractions;

namespace BpmPlus.ExempleClient.Commands;

/// <summary>
/// Commande du noeud "valider-commande".
/// Définit l'identité de la commande. La logique est dans ValiderCommandeHandler.
/// Convention : PascalCase("valider-commande") + "Command" = "ValiderCommandeCommand".
/// </summary>
public record ValiderCommandeCommand : IBpmCommande
{
    public string NomCommande => "ValiderCommandeCommand";
}
