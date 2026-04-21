using BpmPlus.Abstractions;

namespace BpmPlus.Tests.Integration.Handlers;

public class TraiterApresDelaiHandler : IBpmHandlerCommande
{
    public string NomCommande => "TraiterApresDelaiCommand";

    public Task ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        contexte.Variables.Definir("traite_apres_delai", true);
        return Task.CompletedTask;
    }
}
