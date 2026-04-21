using BpmPlus.Abstractions;

namespace BpmPlus.Tests.Integration.Handlers;

public class FinaliserDossierHandler : IBpmHandlerCommande
{
    public string NomCommande => "FinaliserDossierCommand";

    public Task ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        contexte.Variables.Definir("etape", "finalise");
        return Task.CompletedTask;
    }
}
