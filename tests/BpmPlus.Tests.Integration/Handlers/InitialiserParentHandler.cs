using BpmPlus.Abstractions;

namespace BpmPlus.Tests.Integration.Handlers;

public class InitialiserParentHandler : IBpmHandlerCommande
{
    public string NomCommande => "InitialiserParentCommand";

    public Task ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        contexte.Variables.Definir("parent_init", true);
        return Task.CompletedTask;
    }
}
