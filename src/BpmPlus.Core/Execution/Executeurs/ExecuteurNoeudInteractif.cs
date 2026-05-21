using BpmPlus.Abstractions;
using Microsoft.Extensions.Logging;

namespace BpmPlus.Core.Execution.Executeurs;

public class ExecuteurNoeudInteractif
{
    private readonly ExecuteurNoeudMetier _executeurCommande;
    private readonly IGestionTache? _gestionTache;
    private readonly ResolveurParametre _resolveur;
    private readonly ILogger<ExecuteurNoeudInteractif> _logger;

    public ExecuteurNoeudInteractif(
        ExecuteurNoeudMetier executeurCommande,
        IGestionTache? gestionTache,
        ResolveurParametre resolveur,
        ILogger<ExecuteurNoeudInteractif> logger)
    {
        _executeurCommande = executeurCommande;
        _gestionTache = gestionTache;
        _resolveur = resolveur;
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

        string? logon = null;
        if (_gestionTache is not null)
        {
            if (noeud.DefinitionTache.SourceLogonAuto is not null)
            {
                logon = (await _resolveur.ResolveAsync(noeud.DefinitionTache.SourceLogonAuto, contexte, ct))?.ToString();
                if (string.IsNullOrWhiteSpace(logon))
                    logon = null;
            }

            await _gestionTache.CreerTacheAsync(noeud.DefinitionTache, instance, logon, ct);
            _logger.LogInformation("NoeudInteractif '{Id}' — tâche créée pour instance {IdInstance}{LogonAuto}",
                noeud.Id, instance.Id, logon is not null ? $", assignée auto : {logon}" : string.Empty);

            if (logon is not null && noeud.DefinitionTache.NomVariableLogonAssigne is { } nomVarAssigne)
                contexte.Variables.Definir(nomVarAssigne, logon);
        }

        var detail = System.Text.Json.JsonSerializer.Serialize(new
        {
            noeudId = noeud.Id,
            logon
        });

        return new ResultatNoeud(TypeResultatNoeud.Suspendu, null, detail);
    }

    /// <summary>Complétion de la tâche — exécute POST, ferme la tâche, reprend le flux.</summary>
    public async Task<ResultatNoeud> CompleterAsync(
        NoeudInteractif noeud,
        InstanceProcessus instance,
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

        if (noeud.DefinitionTache.NomVariableLogonTachePrecedente is { } nomVarPrec)
        {
            var logonAssigne = noeud.DefinitionTache.NomVariableLogonAssigne is { } nomVarAssigne
                ? contexte.Variables.ObtenirOuDefaut<string?>(nomVarAssigne)
                : null;
            contexte.Variables.Definir(nomVarPrec, logonAssigne);
        }

        if (_gestionTache is not null)
            await _gestionTache.FermerTacheAsync(instance, contexte.Variables.ObtenirToutes(), ct);

        if (noeud.EstFinale)
            return new ResultatNoeud(TypeResultatNoeud.Termine, null);

        var suivant = noeud.FluxSortants.FirstOrDefault()?.Vers;
        return new ResultatNoeud(TypeResultatNoeud.Suivant, suivant);
    }
}
