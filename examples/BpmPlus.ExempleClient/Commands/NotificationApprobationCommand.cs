using BpmPlus.Abstractions;

namespace BpmPlus.ExempleClient.Commands;

/// <summary>
/// Commande du noeud final "notification-approbation".
/// Convention : PascalCase("notification-approbation") + "Command" = "NotificationApprobationCommand".
/// </summary>
public record NotificationApprobationCommand : IBpmCommande
{
    public string NomCommande => "NotificationApprobationCommand";
}
