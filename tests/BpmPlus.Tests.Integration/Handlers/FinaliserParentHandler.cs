using BpmPlus.Abstractions;

namespace BpmPlus.Tests.Integration.Handlers;

public class FinaliserParentHandler : IBpmHandlerCommande
{
    public string NomCommande => "FinaliserParentCommand";

    public Task ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        contexte.Variables.Definir("parent_final", true);
        return Task.CompletedTask;
    }
}
