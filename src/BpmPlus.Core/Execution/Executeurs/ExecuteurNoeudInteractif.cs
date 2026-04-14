using BpmPlus.Abstractions;
using Microsoft.Extensions.Logging;

namespace BpmPlus.Core.Execution.Executeurs;

public class ExecuteurNoeudInteractif
{
    private readonly ExecuteurNoeudMetier _executeurCommande;
    private readonly IGestionTache? _gestionTache;
    private readonly ILogger<ExecuteurNoeudInteractif> _logger;

    public ExecuteurNoeudInteractif(
        ExecuteurNoeudMetier executeurCommande,
        IGestionTache? gestionTache,
        ILogger<ExecuteurNoeudInteractif> logger)
    {
        _executeurCommande = executeurCommande;
        _gestionTache = gestionTache;
        _logger = logger;
    }

    /// <summary>Arrivée initiale sur le nœud interactif — exécute PRE, crée la tâche, suspend.</summary>
    public async Task<ResultatNoeud> EntrerAsync(
        NoeudInteractif noeud,
        InstanceProcessus instance,
        IContexteExecution contexte,
        CancellationToken ct)
    {
        _logger.LogInformation("NoeudInteractif '{Id}' — suspension et création de tâche", noeud.Id);

        if (noeud.CommandePre is not null)
        {
            _logger.LogInformation("NoeudInteractif '{Id}' — exécution CommandePre '{Cmd}'",
                noeud.Id, noeud.CommandePre.NomCommande);
            await _executeurCommande.ExecuterDefinitionCommandeAsync(noeud.CommandePre, contexte, ct);
        }

        string? idTacheExterne = null;
        if (_gestionTache is not null)
        {
            idTacheExterne = await _gestionTache.CreerTacheAsync(noeud.DefinitionTache, instance, ct);
            _logger.LogInformation("NoeudInteractif '{Id}' — tâche créée : {IdTache}", noeud.Id, idTacheExterne);
        }

        var detail = System.Text.Json.JsonSerializer.Serialize(new
        {
            idTacheExterne,
            noeudId = noeud.Id
        });

        return new ResultatNoeud(TypeResultatNoeud.Suspendu, null, detail);
    }

    /// <summary>Complétion de la tâche — exécute POST, ferme la tâche, reprend le flux.</summary>
    public async Task<ResultatNoeud> CompleterAsync(
        NoeudInteractif noeud,
        string? idTacheExterne,
        IContexteExecution contexte,
        CancellationToken ct)
    {
        _logger.LogInformation("NoeudInteractif '{Id}' — complétion, reprise", noeud.Id);

        if (noeud.CommandePost is not null)
        {
            _logger.LogInformation("NoeudInteractif '{Id}' — exécution CommandePost '{Cmd}'",
                noeud.Id, noeud.CommandePost.NomCommande);
            await _executeurCommande.ExecuterDefinitionCommandeAsync(noeud.CommandePost, contexte, ct);
        }

        if (_gestionTache is not null && idTacheExterne is not null)
            await _gestionTache.FermerTacheAsync(idTacheExterne, ct);

        if (noeud.EstFinale)
            return new ResultatNoeud(TypeResultatNoeud.Termine, null);

        var suivant = noeud.FluxSortants.FirstOrDefault()?.Vers;
        return new ResultatNoeud(TypeResultatNoeud.Suivant, suivant);
    }
}
