using BpmPlus.Abstractions;

namespace BpmPlus.Tests.Integration.Handlers;

public class NotifierRefusHandler : IBpmHandlerCommande
{
    public string NomCommande => "NotifierRefusCommand";

    public Task ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        contexte.Variables.Definir("notification", "refus");
        return Task.CompletedTask;
    }
}
