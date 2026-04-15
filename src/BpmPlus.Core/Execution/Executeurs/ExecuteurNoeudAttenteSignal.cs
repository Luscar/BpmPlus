using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Microsoft.Extensions.Logging;

namespace BpmPlus.Core.Execution.Executeurs;

public class ExecuteurNoeudAttenteSignal
{
    private readonly IRepositoryAttenteSignal _repoSignal;
    private readonly ILogger<ExecuteurNoeudAttenteSignal> _logger;

    public ExecuteurNoeudAttenteSignal(
        IRepositoryAttenteSignal repoSignal,
        ILogger<ExecuteurNoeudAttenteSignal> logger)
    {
        _repoSignal = repoSignal;
        _logger = logger;
    }

    public async Task<ResultatNoeud> EntrerAsync(
        NoeudAttenteSignal noeud,
        long idInstance,
        CancellationToken ct)
    {
        _logger.LogInformation("NoeudAttenteSignal '{Id}' — attente signal '{Signal}'",
            noeud.Id, noeud.NomSignal);

        await _repoSignal.AjouterAsync(idInstance, noeud.NomSignal, ct);

        var detail = System.Text.Json.JsonSerializer.Serialize(new
        {
            typeAttente = "Signal",
            nomSignal = noeud.NomSignal,
            noeudId = noeud.Id
        });

        return new ResultatNoeud(TypeResultatNoeud.Suspendu, null, detail);
    }

    public async Task<ResultatNoeud> ReprendreAsync(
        NoeudAttenteSignal noeud,
        long idInstance,
        CancellationToken ct)
    {
        _logger.LogInformation("NoeudAttenteSignal '{Id}' — signal reçu, reprise", noeud.Id);
        await _repoSignal.SupprimerParInstanceAsync(idInstance, ct);

        if (noeud.EstFinale)
            return new ResultatNoeud(TypeResultatNoeud.Termine, null);

        var suivant = noeud.FluxSortants.FirstOrDefault()?.Vers;
        return new ResultatNoeud(TypeResultatNoeud.Suivant, suivant);
    }
}
