using BpmPlus.Abstractions;
using Microsoft.Extensions.Logging;

namespace BpmPlus.Core.Execution.Executeurs;

public class ExecuteurNoeudQuery
{
    private readonly ResolveurParametre _resolveur;
    private readonly ILogger<ExecuteurNoeudQuery> _logger;

    public ExecuteurNoeudQuery(
        ResolveurParametre resolveur,
        ILogger<ExecuteurNoeudQuery> logger)
    {
        _resolveur = resolveur;
        _logger = logger;
    }

    public async Task<ResultatNoeud> ExecuterAsync(
        NoeudQuery noeud,
        IContexteExecution contexte,
        CancellationToken ct)
    {
        _logger.LogInformation("Exécution NoeudQuery '{NomQuery}' → '{Variable}' (nœud '{Id}')",
            noeud.NomQuery, noeud.NomVariableResultat, noeud.Id);

        var source = new SourceQuery(noeud.NomQuery, noeud.Parametres.Count > 0 ? noeud.Parametres : null);
        var resultat = await _resolveur.ResolveAsync(source, contexte, ct);

        if (!string.IsNullOrEmpty(noeud.NomVariableResultat))
            contexte.Variables.Definir(noeud.NomVariableResultat, resultat);

        if (noeud.EstFinale || noeud.FluxSortants.Count == 0)
            return new ResultatNoeud(TypeResultatNoeud.Termine, null);

        var suivant = noeud.FluxSortants.FirstOrDefault()?.Vers;
        return new ResultatNoeud(TypeResultatNoeud.Suivant, suivant);
    }
}
