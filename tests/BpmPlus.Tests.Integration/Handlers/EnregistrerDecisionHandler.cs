using BpmPlus.Abstractions;

namespace BpmPlus.Tests.Integration.Handlers;

/// <summary>
/// Commande post-tâche : enregistre la date de décision (utilisée dans les tests d'approbation).
/// </summary>
public class EnregistrerDecisionHandler : IBpmHandlerCommande
{
    public string NomCommande => "EnregistrerDecisionCommand";

    public Task ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        contexte.Variables.Definir("date_decision", DateTime.UtcNow);
        return Task.CompletedTask;
    }
}
