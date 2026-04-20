using BpmPlus.Abstractions;

namespace BpmPlus.Tests.Integration.Handlers;

public class InitialiserDossierHandler : IBpmHandlerCommande
{
    public string NomCommande => "InitialiserDossierCommand";

    public Task ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        contexte.Variables.Definir("etape", "initialise");
        contexte.Variables.Definir("dossier_ref", $"DOS-{aggregateId}");
        return Task.CompletedTask;
    }
}
