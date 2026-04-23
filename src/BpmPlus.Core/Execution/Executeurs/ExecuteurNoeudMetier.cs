using BpmPlus.Abstractions;
using Microsoft.Extensions.Logging;

namespace BpmPlus.Core.Execution.Executeurs;

public class ExecuteurNoeudMetier
{
    private readonly IBpmServiceResolver _resolver;
    private readonly ResolveurParametre _resolveur;
    private readonly ILogger<ExecuteurNoeudMetier> _logger;

    public ExecuteurNoeudMetier(
        IBpmServiceResolver resolver,
        ResolveurParametre resolveur,
        ILogger<ExecuteurNoeudMetier> logger)
    {
        _resolver = resolver;
        _resolveur = resolveur;
        _logger = logger;
    }

    public async Task<ResultatNoeud> ExecuterAsync(
        NoeudMetier noeud,
        IContexteExecution contexte,
        CancellationToken ct)
    {
        _logger.LogInformation("Exécution NoeudMetier '{NomCommande}' (nœud '{Id}')",
            noeud.NomCommande, noeud.Id);

        var handler = _resolver.GetCommande(noeud.NomCommande)
            ?? throw new InvalidOperationException(
                $"Aucun IBpmHandlerCommande enregistré pour la commande '{noeud.NomCommande}'.");

        var parametres = await _resolveur.ResolveParametresAsync(noeud.Parametres, contexte, ct);

        await handler.ExecuterAsync(contexte.IdInstance, contexte.AggregateId, parametres, contexte);

        if (noeud.EstFinale)
            return new ResultatNoeud(TypeResultatNoeud.Termine, null);

        var suivant = noeud.FluxSortants.FirstOrDefault()?.Vers;
        return new ResultatNoeud(TypeResultatNoeud.Suivant, suivant);
    }

    public async Task ExecuterDefinitionCommandeAsync(
        DefinitionCommande def,
        IContexteExecution contexte,
        CancellationToken ct)
    {
        var handler = _resolver.GetCommande(def.NomCommande)
            ?? throw new InvalidOperationException(
                $"Aucun IBpmHandlerCommande enregistré pour la commande '{def.NomCommande}'.");

        var parametres = await _resolveur.ResolveParametresAsync(def.Parametres, contexte, ct);

        await handler.ExecuterAsync(contexte.IdInstance, contexte.AggregateId, parametres, contexte);
    }
}
