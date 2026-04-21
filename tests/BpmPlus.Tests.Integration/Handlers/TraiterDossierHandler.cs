using BpmPlus.Abstractions;

namespace BpmPlus.Tests.Integration.Handlers;

public class TraiterDossierHandler : IBpmHandlerCommande
{
    public string NomCommande => "TraiterDossierCommand";

    public Task ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        contexte.Variables.Definir("etape", "traite");
        return Task.CompletedTask;
    }
}
