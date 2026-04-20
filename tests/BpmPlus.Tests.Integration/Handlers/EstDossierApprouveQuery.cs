using BpmPlus.Abstractions;

namespace BpmPlus.Tests.Integration.Handlers;

public class EstDossierApprouveQuery : IBpmHandlerQuery<bool>
{
    public string NomQuery => "EstDossierApprouveQuery";

    public Task<bool> ExecuterAsync(
        long idInstance,
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        var approbation = contexte.Variables.ObtenirOuDefaut<string>("approbation");
        return Task.FromResult(approbation == "Approuve");
    }
}
