namespace BpmPlus.Abstractions;

/// <summary>
/// Marqueur d'identité d'une commande BPM.
/// La commande définit le "quoi" (NomCommande), séparée du handler qui définit le "comment".
/// </summary>
public interface IBpmCommande
{
    string NomCommande { get; }
}
