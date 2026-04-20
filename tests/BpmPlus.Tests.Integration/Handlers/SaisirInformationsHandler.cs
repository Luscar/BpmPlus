using BpmPlus.Abstractions;

namespace BpmPlus.Tests.Integration.Handlers;

public class SaisirInformationsHandler : IBpmHandlerCommande
{
    public string NomCommande => "SaisirInformationsCommand";

    public Task ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        contexte.Variables.Definir("resultat_enfant", "OK");
        contexte.Variables.Definir("infos_saisies", true);
        return Task.CompletedTask;
    }
}
