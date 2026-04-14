using Autofac;
using BpmPlus.Abstractions;
using Microsoft.Extensions.Logging;

namespace BpmPlus.Core.Execution.Executeurs;

public class ExecuteurNoeudMetier
{
    private readonly ILifetimeScope _scope;
    private readonly ResolveurParametre _resolveur;
    private readonly ILogger<ExecuteurNoeudMetier> _logger;

    public ExecuteurNoeudMetier(
        ILifetimeScope scope,
        ResolveurParametre resolveur,
        ILogger<ExecuteurNoeudMetier> logger)
    {
        _scope = scope;
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

        var handler = _scope.ResolveKeyed<IHandlerCommande>(noeud.NomCommande)
            ?? throw new InvalidOperationException(
                $"Aucun IHandlerCommande enregistré pour la commande '{noeud.NomCommande}'.");

        var aggregateId = await _resolveur.ResolveAggregateIdAsync(noeud.SourceAggregateId, contexte, ct);
        var parametres = await _resolveur.ResolveParametresAsync(noeud.Parametres, contexte, ct);

        await handler.ExecuterAsync(aggregateId, parametres, contexte);

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
        var handler = _scope.ResolveKeyed<IHandlerCommande>(def.NomCommande)
            ?? throw new InvalidOperationException(
                $"Aucun IHandlerCommande enregistré pour la commande '{def.NomCommande}'.");

        var aggregateId = await _resolveur.ResolveAggregateIdAsync(def.SourceAggregateId, contexte, ct);
        var parametres = await _resolveur.ResolveParametresAsync(def.Parametres, contexte, ct);

        await handler.ExecuterAsync(aggregateId, parametres, contexte);
    }
}
