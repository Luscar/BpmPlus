using BpmPlus.Abstractions;

namespace BpmPlus.ExempleClient.Commands;

/// <summary>
/// Commande du noeud final "notification-refus".
/// Convention : PascalCase("notification-refus") + "Command" = "NotificationRefusCommand".
/// </summary>
public record NotificationRefusCommand : IBpmCommande
{
    public string NomCommande => "NotificationRefusCommand";
}
