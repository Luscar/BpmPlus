namespace BpmPlus.Abstractions;

/// <summary>
/// Marqueur d'identité d'une query BPM.
/// La query définit le "quoi" (NomQuery) et le type du résultat,
/// séparée du handler qui contient la logique d'évaluation.
/// </summary>
public interface IBpmQuery<TResultat>
{
    string NomQuery { get; }
}
