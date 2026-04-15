using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Microsoft.Extensions.Logging;

namespace BpmPlus.Core.Execution.Executeurs;

public class ExecuteurNoeudSousProcessus
{
    private readonly IDbSession _session;
    private readonly IRepositoryDefinition _repoDefinition;
    private readonly IRepositoryInstance _repoInstance;
    private readonly IRepositoryVariable _repoVariable;
    private readonly Func<MoteurExecution> _moteurFactory;
    private readonly ILogger<ExecuteurNoeudSousProcessus> _logger;

    public ExecuteurNoeudSousProcessus(
        IDbSession session,
        IRepositoryDefinition repoDefinition,
        IRepositoryInstance repoInstance,
        IRepositoryVariable repoVariable,
        Func<MoteurExecution> moteurFactory,
        ILogger<ExecuteurNoeudSousProcessus> logger)
    {
        _session = session;
        _repoDefinition = repoDefinition;
        _repoInstance = repoInstance;
        _repoVariable = repoVariable;
        _moteurFactory = moteurFactory;
        _logger = logger;
    }

    public async Task<ResultatNoeud> ExecuterAsync(
        NoeudSousProcessus noeud,
        InstanceProcessus instanceParente,
        IContexteExecution contexteParent,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "NoeudSousProcessus '{Id}' — démarrage sous-processus '{Cle}' v{Version}",
            noeud.Id, noeud.CleDefinition, noeud.Version);

        var definitionEnfant = await _repoDefinition.ObtenirVersionPublieeAsync(
            noeud.CleDefinition, noeud.Version, ct)
            ?? throw new DefinitionIntrouvableException(noeud.CleDefinition, noeud.Version);

        var maintenant = DateTime.UtcNow;
        var instanceEnfant = new InstanceProcessus
        {
            CleDefinition = noeud.CleDefinition,
            VersionDefinition = noeud.Version,
            AggregateId = instanceParente.AggregateId,
            Statut = StatutInstance.Active,
            IdInstanceParent = instanceParente.Id,
            DateDebut = maintenant,
            DateCreation = maintenant,
            DateMaj = maintenant
        };

        var idEnfant = await _repoInstance.CreerAsync(instanceEnfant, ct);
        instanceEnfant.Id = idEnfant;

        var variablesParent = contexteParent.Variables.ObtenirToutes();
        var variablesEnfant = new Dictionary<string, object?>(variablesParent);
        await _repoVariable.SauvegarderToutesAsync(idEnfant, variablesEnfant, ct);

        var accesseurEnfant = new AccesseurVariables(variablesEnfant);
        var contexteEnfant = new ContexteExecution(
            idEnfant,
            noeud.CleDefinition,
            noeud.Version,
            instanceParente.AggregateId,
            _session,
            accesseurEnfant,
            ct);

        var resultatEnfant = await _moteurFactory().ExecuterDepuisDebutAsync(
            definitionEnfant, instanceEnfant, contexteEnfant, ct);

        if (resultatEnfant == TypeResultatExecution.Suspendu)
        {
            _logger.LogInformation(
                "NoeudSousProcessus '{Id}' — sous-processus suspendu, parent suspendu aussi", noeud.Id);
            var detail = System.Text.Json.JsonSerializer.Serialize(new
            {
                typeAttente = "SousProcessus",
                idInstanceEnfant = idEnfant,
                noeudId = noeud.Id
            });
            return new ResultatNoeud(TypeResultatNoeud.Suspendu, null, detail);
        }

        var variablesFinalesEnfant = accesseurEnfant.ObtenirToutes();
        foreach (var nomVariable in noeud.VariablesSorties)
        {
            if (variablesFinalesEnfant.TryGetValue(nomVariable, out var valeur))
            {
                contexteParent.Variables.Definir(nomVariable, valeur);
                _logger.LogDebug(
                    "Variable '{Nom}' remontée du sous-processus {IdEnfant} vers le parent", nomVariable, idEnfant);
            }
        }

        _logger.LogInformation("NoeudSousProcessus '{Id}' — sous-processus terminé", noeud.Id);

        if (noeud.EstFinale)
            return new ResultatNoeud(TypeResultatNoeud.Termine, null);

        var suivant = noeud.FluxSortants.FirstOrDefault()?.Vers;
        return new ResultatNoeud(TypeResultatNoeud.Suivant, suivant);
    }
}
