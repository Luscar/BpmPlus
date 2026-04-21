using BpmPlus.Abstractions;

namespace BpmPlus.Tests.Integration.Handlers;

public class NotifierApprobationHandler : IBpmHandlerCommande
{
    public string NomCommande => "NotifierApprobationCommand";

    public Task ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        contexte.Variables.Definir("notification", "approbation");
        return Task.CompletedTask;
    }
}
