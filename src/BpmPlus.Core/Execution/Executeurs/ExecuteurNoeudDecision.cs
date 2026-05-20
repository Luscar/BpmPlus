using BpmPlus.Abstractions;
using Microsoft.Extensions.Logging;

namespace BpmPlus.Core.Execution.Executeurs;

public class ExecuteurNoeudDecision
{
    private readonly ResolveurParametre _resolveur;
    private readonly ILogger<ExecuteurNoeudDecision> _logger;

    public ExecuteurNoeudDecision(ResolveurParametre resolveur, ILogger<ExecuteurNoeudDecision> logger)
    {
        _resolveur = resolveur;
        _logger = logger;
    }

    public async Task<ResultatNoeud> ExecuterAsync(
        NoeudDecision noeud,
        IContexteExecution contexte,
        CancellationToken ct)
    {
        _logger.LogInformation("Évaluation NoeudDecision '{Id}'", noeud.Id);

        FluxSortant? brancheDefaut = null;

        foreach (var flux in noeud.FluxSortants)
        {
            if (flux.EstParDefaut || flux.Condition is null)
            {
                brancheDefaut = flux;
                continue;
            }

            var resultat = await _resolveur.EvaluerConditionAsync(flux.Condition, contexte, ct);
            if (resultat)
            {
                _logger.LogInformation("Décision '{Id}' → branche '{Vers}'", noeud.Id, flux.Vers);
                return ResoudreFlux(noeud.Id, flux);
            }
        }

        if (brancheDefaut is not null)
        {
            _logger.LogWarning("Décision '{Id}' → aucune condition vraie, branche par défaut '{Vers}'",
                noeud.Id, brancheDefaut.Vers);
            return ResoudreFlux(noeud.Id, brancheDefaut);
        }

        throw new AucunCheminException(noeud.Id);
    }

    private ResultatNoeud ResoudreFlux(string noeudId, FluxSortant flux)
    {
        if (flux.EstTerminal)
        {
            _logger.LogInformation("Décision '{Id}' → branche terminale, fin du processus", noeudId);
            return new ResultatNoeud(TypeResultatNoeud.Termine, null);
        }
        return new ResultatNoeud(TypeResultatNoeud.Suivant, flux.Vers);
    }
}
