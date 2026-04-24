using System.Text.Json;
using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;
using BpmPlus.Core.Execution;
using BpmPlus.Core.Execution.Executeurs;
using BpmPlus.Core.Persistance;
using Microsoft.Extensions.Logging;

namespace BpmPlus.Core.Services;

public class ServiceBpm : IServiceBpm
{
    private readonly IRepositoryDefinition _repoDefinition;
    private readonly IRepositoryInstance _repoInstance;
    private readonly IRepositoryVariable _repoVariable;
    private readonly IRepositoryEvenement _repoEvenement;
    private readonly IRepositoryAttenteSignal _repoSignal;
    private readonly IGestionTache _gestionTache;
    private readonly MoteurExecution _moteur;
    private readonly ExecuteurNoeudInteractif _executeurInteractif;
    private readonly ExecuteurNoeudAttenteTemps _executeurAttenteTemps;
    private readonly ExecuteurNoeudAttenteSignal _executeurAttenteSignal;
    private readonly ILogger<ServiceBpm> _logger;

    public ServiceBpm(
        IRepositoryDefinition repoDefinition,
        IRepositoryInstance repoInstance,
        IRepositoryVariable repoVariable,
        IRepositoryEvenement repoEvenement,
        IRepositoryAttenteSignal repoSignal,
        IGestionTache gestionTache,
        MoteurExecution moteur,
        ExecuteurNoeudInteractif executeurInteractif,
        ExecuteurNoeudAttenteTemps executeurAttenteTemps,
        ExecuteurNoeudAttenteSignal executeurAttenteSignal,
        ILogger<ServiceBpm> logger)
    {
        _repoDefinition = repoDefinition;
        _repoInstance = repoInstance;
        _repoVariable = repoVariable;
        _repoEvenement = repoEvenement;
        _repoSignal = repoSignal;
        _gestionTache = gestionTache;
        _moteur = moteur;
        _executeurInteractif = executeurInteractif;
        _executeurAttenteTemps = executeurAttenteTemps;
        _executeurAttenteSignal = executeurAttenteSignal;
        _logger = logger;
    }

    // ── Instances ─────────────────────────────────────────────────────────────

    public async Task<long> DemarrerAsync(
        string cleDefinition,
        long aggregateId,
        IReadOnlyDictionary<string, object?>? variablesInitiales,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Démarrage processus '{Cle}' pour l'agrégat {AggId}", cleDefinition, aggregateId);

        // Vérifier l'unicité
        if (await _repoInstance.ExisteProcessusActifAsync(cleDefinition, aggregateId, ct))
            throw new ProcessusDejaActifException(cleDefinition, aggregateId);

        // Charger la définition publiée la plus récente
        var definition = await _repoDefinition.ObtenirDerniereVersionPublieeAsync(cleDefinition, ct)
            ?? throw new DefinitionIntrouvableException(cleDefinition);

        // Créer l'instance
        var maintenant = DateTime.UtcNow;
        var instance = new InstanceProcessus
        {
            CleDefinition = cleDefinition,
            VersionDefinition = definition.Version,
            AggregateId = aggregateId,
            Statut = StatutInstance.Active,
            DateDebut = maintenant,
            DateCreation = maintenant,
            DateMaj = maintenant
        };

        var idInstance = await _repoInstance.CreerAsync(instance, ct);
        instance.Id = idInstance;

        _logger.LogInformation("Instance {IdInstance} créée — processus '{Cle}' v{Version}, agrégat {AggId}",
            idInstance, cleDefinition, definition.Version, aggregateId);

        // Persister les variables initiales
        var variables = variablesInitiales != null
            ? new Dictionary<string, object?>(variablesInitiales)
            : new Dictionary<string, object?>();

        await _repoVariable.SauvegarderToutesAsync(idInstance, variables, ct);

        // Enregistrer l'événement de début
        await _repoEvenement.AjouterAsync(new EvenementInstance
        {
            IdInstance = idInstance,
            TypeEvenement = TypeEvenement.DebutProcessus,
            Horodatage = maintenant,
            Resultat = ResultatEvenement.Succes,
            Detail = $"Instance {idInstance} — processus '{cleDefinition}' v{definition.Version}, agrégat {aggregateId}"
        }, ct);

        // Construire le contexte et exécuter
        var accesseur = new AccesseurVariables(variables);
        var contexte = new ContexteExecution(
            idInstance, cleDefinition, definition.Version,
            aggregateId, accesseur, ct);

        await _moteur.ExecuterDepuisDebutAsync(definition, instance, contexte, ct);

        return idInstance;
    }

    public async Task<InstanceProcessus> ObtenirAsync(long idInstance, CancellationToken ct = default)
    {
        return await _repoInstance.ObtenirParIdAsync(idInstance, ct)
            ?? throw new KeyNotFoundException($"Instance {idInstance} introuvable.");
    }

    public Task<InstanceProcessus?> ObtenirParAggregateAsync(
        string cleDefinition, long aggregateId, CancellationToken ct = default)
        => _repoInstance.ObtenirActiveParAggregateAsync(cleDefinition, aggregateId, ct);

    public async Task<IReadOnlyList<InstanceProcessus>> RechercherParVariableAsync(
        string nomVariable, object valeur, CancellationToken ct = default)
    {
        var valeurSerialisee = SerialiserValeur(valeur);
        return await _repoInstance.RechercherParVariableAsync(nomVariable, valeurSerialisee, ct);
    }

    public async Task<IReadOnlyList<InstanceProcessus>> RechercherParVariableAsync(
        string nomVariable, object valeur, StatutInstance statut, CancellationToken ct = default)
    {
        var valeurSerialisee = SerialiserValeur(valeur);
        return await _repoInstance.RechercherParVariableAsync(nomVariable, valeurSerialisee, statut, ct);
    }

    public Task<IReadOnlyList<InstanceProcessus>> ObtenirInstancesSuspenduesAsync(CancellationToken ct = default)
        => _repoInstance.ObtenirParStatutAsync(StatutInstance.Suspendue, ct);

    public Task<IReadOnlyList<InstanceProcessus>> ObtenirEnfantsAsync(
        long idInstanceParent, CancellationToken ct = default)
        => _repoInstance.ObtenirEnfantsAsync(idInstanceParent, ct);

    // ── Étapes et reprise ─────────────────────────────────────────────────────

    public async Task TerminerEtapeAsync(long idInstance, CancellationToken ct = default)
    {
        var instance = await ObtenirInstanceValideAsync(idInstance, StatutInstance.Suspendue, ct);

        var definition = await ChargerDefinitionInstanceAsync(instance, ct);
        var noeudId = instance.IdNoeudCourant
            ?? throw new EtatInstanceInvalideException(idInstance, instance.Statut,
                "Aucun nœud courant pour une instance suspendue.");

        var noeud = definition.TrouverNoeud(noeudId) as NoeudInteractif
            ?? throw new EtatInstanceInvalideException(idInstance, instance.Statut,
                $"Le nœud courant '{noeudId}' n'est pas un NoeudInteractif.");

        var variables = await _repoVariable.ChargerToutesAsync(idInstance, ct);
        var accesseur = new AccesseurVariables(variables);
        var contexte = new ContexteExecution(
            idInstance, instance.CleDefinition, instance.VersionDefinition,
            instance.AggregateId, accesseur, ct);

        var idTacheExterne = await ExtraireIdTacheExterneAsync(idInstance, ct);

        await _repoEvenement.AjouterAsync(new EvenementInstance
        {
            IdInstance = idInstance,
            TypeEvenement = TypeEvenement.NoeudRepris,
            IdNoeud = noeudId,
            NomNoeud = noeud.Nom,
            Horodatage = DateTime.UtcNow,
            Resultat = ResultatEvenement.Succes
        }, ct);

        var resultat = await _executeurInteractif.CompleterAsync(noeud, idTacheExterne, instance, contexte, ct);
        await ContinuerApresReprise(resultat, instance, definition, noeudId, accesseur, contexte, ct);
    }

    public async Task EnvoyerSignalAsync(
        string nomSignal, long? idInstance = null, CancellationToken ct = default)
    {
        if (idInstance.HasValue)
        {
            await EnvoyerSignalCibleAsync(idInstance.Value, nomSignal, ct);
        }
        else
        {
            var instances = await _repoSignal.ObtenirInstancesEnAttenteAsync(nomSignal, ct);
            _logger.LogInformation("Signal broadcast '{Signal}' → {Count} instance(s)", nomSignal, instances.Count);
            foreach (var id in instances)
                await EnvoyerSignalCibleAsync(id, nomSignal, ct);
        }
    }

    public async Task ReprendreAttenteTempsAsync(long idInstance, CancellationToken ct = default)
    {
        var instance = await ObtenirInstanceValideAsync(idInstance, StatutInstance.Suspendue, ct);
        var definition = await ChargerDefinitionInstanceAsync(instance, ct);
        var noeudId = instance.IdNoeudCourant
            ?? throw new EtatInstanceInvalideException(idInstance, instance.Statut,
                "Aucun nœud courant.");

        var noeud = definition.TrouverNoeud(noeudId) as NoeudAttenteTemps
            ?? throw new EtatInstanceInvalideException(idInstance, instance.Statut,
                $"Le nœud courant '{noeudId}' n'est pas un NoeudAttenteTemps.");

        var variables = await _repoVariable.ChargerToutesAsync(idInstance, ct);
        var accesseur = new AccesseurVariables(variables);
        var contexte = new ContexteExecution(
            idInstance, instance.CleDefinition, instance.VersionDefinition,
            instance.AggregateId, accesseur, ct);

        await _repoEvenement.AjouterAsync(new EvenementInstance
        {
            IdInstance = idInstance,
            TypeEvenement = TypeEvenement.NoeudRepris,
            IdNoeud = noeudId,
            NomNoeud = noeud.Nom,
            Horodatage = DateTime.UtcNow,
            Resultat = ResultatEvenement.Succes
        }, ct);

        var resultat = _executeurAttenteTemps.Reprendre(noeud);
        await ContinuerApresReprise(resultat, instance, definition, noeudId, accesseur, contexte, ct);
    }

    public async Task<IReadOnlyList<InstanceEchue>> ObtenirInstancesEchuesAsync(
        DateTime dateReference, CancellationToken ct = default)
    {
        var suspendues = await _repoInstance.ObtenirSuspenduesAsync(ct);
        var echues = new List<InstanceEchue>();

        foreach (var instance in suspendues)
        {
            if (instance.IdNoeudCourant is null) continue;

            var dernieresSuspensions = await _repoEvenement.ObtenirDernierSuspensionAsync(
                instance.Id, ct);
            if (dernieresSuspensions?.Detail is null) continue;

            try
            {
                using var doc = JsonDocument.Parse(dernieresSuspensions.Detail);
                if (!doc.RootElement.TryGetProperty("typeAttente", out var typeAttente)) continue;
                if (typeAttente.GetString() != "Temps") continue;

                if (!doc.RootElement.TryGetProperty("dateEcheance", out var dateEchEl)) continue;
                if (!DateTime.TryParse(dateEchEl.GetString(), out var dateEcheance)) continue;

                if (dateEcheance <= dateReference)
                    echues.Add(new InstanceEchue(instance.Id, dateEcheance));
            }
            catch (JsonException) { /* ignorer les détails mal formés */ }
        }

        return echues;
    }

    // ── Variables ─────────────────────────────────────────────────────────────

    public async Task ModifierVariableAsync(
        long idInstance, string nomVariable, object? valeur, CancellationToken ct = default)
    {
        _logger.LogInformation("Modification variable '{Nom}' de l'instance {Id}", nomVariable, idInstance);

        await _repoVariable.MettreAJourAsync(idInstance, nomVariable, valeur, ct);

        await _repoEvenement.AjouterAsync(new EvenementInstance
        {
            IdInstance = idInstance,
            TypeEvenement = TypeEvenement.VariableModifiee,
            Horodatage = DateTime.UtcNow,
            Detail = $"Variable '{nomVariable}' modifiée : {SerialiserValeur(valeur ?? string.Empty)}"
        }, ct);
    }

    // ── Signaux ────────────────────────────────────────────────────────────────

    public Task<IReadOnlyList<string>> ObtenirSignauxEnAttenteAsync(
        long idInstance, CancellationToken ct = default)
        => _repoSignal.ObtenirSignauxParInstanceAsync(idInstance, ct);

    // ── Définitions ───────────────────────────────────────────────────────────

    public async Task SauvegarderDefinitionAsync(
        DefinitionProcessus definition, CancellationToken ct = default)
    {
        _logger.LogInformation("Sauvegarde définition '{Cle}'", definition.Cle);

        var brouillon = await _repoDefinition.ObtenirBrouillonAsync(definition.Cle, ct);

        if (brouillon is not null)
        {
            definition.Id = brouillon.Id;
            definition.Version = brouillon.Version;
        }

        await _repoDefinition.SauvegarderAsync(definition, ct);
    }

    public async Task PublierDefinitionAsync(string cleDefinition, CancellationToken ct = default)
    {
        _logger.LogInformation("Publication définition '{Cle}'", cleDefinition);
        var brouillon = await _repoDefinition.ObtenirBrouillonAsync(cleDefinition, ct)
            ?? throw new DefinitionIntrouvableException(cleDefinition);

        await _repoDefinition.PublierAsync(cleDefinition, ct);
    }

    public Task<IReadOnlyList<DefinitionProcessus>> ObtenirDefinitionsAsync(CancellationToken ct = default)
        => _repoDefinition.ObtenirToutesAsync(ct);

    // ── Tâches ────────────────────────────────────────────────────────────────

    public Task<long?> ObtenirIdTacheActiveAsync(long idInstance, CancellationToken ct = default)
        => ExtraireIdTacheExterneAsync(idInstance, ct);

    public async Task<string?> ObtenirLogonTacheActiveAsync(long idInstance, CancellationToken ct = default)
    {
        var suspension = await _repoEvenement.ObtenirDernierSuspensionAsync(idInstance, ct);
        if (suspension is null) return null;

        var assignation = await _repoEvenement.ObtenirDernierParTypeAsync(
            idInstance, TypeEvenement.TacheAssignee, ct);
        if (assignation is not null && assignation.Id > suspension.Id)
            return assignation.Detail;

        if (suspension.Detail is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(suspension.Detail);
            if (doc.RootElement.TryGetProperty("logon", out var el))
                return el.GetString();
        }
        catch (JsonException) { }

        return null;
    }

    public async Task AssignerLogonAsync(long idInstance, string logon, CancellationToken ct = default)
    {
        var instance = await ObtenirInstanceValideAsync(idInstance, StatutInstance.Suspendue, ct);

        var idTacheExterne = await ExtraireIdTacheExterneAsync(idInstance, ct);
        if (idTacheExterne is not null)
            await _gestionTache.AssignerTacheAsync(idTacheExterne.Value, logon, ct);

        await _repoEvenement.AjouterAsync(new EvenementInstance
        {
            IdInstance = idInstance,
            TypeEvenement = TypeEvenement.TacheAssignee,
            IdNoeud = instance.IdNoeudCourant,
            Horodatage = DateTime.UtcNow,
            Resultat = ResultatEvenement.Succes,
            Detail = logon
        }, ct);

        _logger.LogInformation("Instance {IdInstance} — tâche assignée à '{Logon}'", idInstance, logon);
    }

    // ── Historique ────────────────────────────────────────────────────────────

    public Task<IReadOnlyList<EvenementInstance>> ObtenirHistoriqueAsync(
        long idInstance, CancellationToken ct = default)
        => _repoEvenement.ObtenirParInstanceAsync(idInstance, ct);

    public Task<EvenementInstance?> ObtenirDernierEvenementTacheAsync(
        long idInstance, CancellationToken ct = default)
        => _repoEvenement.ObtenirDernierSuspensionAsync(idInstance, ct);

    // ── Méthodes privées ──────────────────────────────────────────────────────

    private async Task EnvoyerSignalCibleAsync(
        long idInstance, string nomSignal, CancellationToken ct)
    {
        _logger.LogInformation("Signal '{Signal}' → instance {Id}", nomSignal, idInstance);

        var instance = await ObtenirInstanceValideAsync(idInstance, StatutInstance.Suspendue, ct);
        var definition = await ChargerDefinitionInstanceAsync(instance, ct);
        var noeudId = instance.IdNoeudCourant
            ?? throw new EtatInstanceInvalideException(idInstance, instance.Statut, "Aucun nœud courant.");

        var noeud = definition.TrouverNoeud(noeudId) as NoeudAttenteSignal
            ?? throw new EtatInstanceInvalideException(idInstance, instance.Statut,
                $"Le nœud courant '{noeudId}' n'est pas un NoeudAttenteSignal.");

        var variables = await _repoVariable.ChargerToutesAsync(idInstance, ct);
        var accesseur = new AccesseurVariables(variables);
        var contexte = new ContexteExecution(
            idInstance, instance.CleDefinition, instance.VersionDefinition,
            instance.AggregateId, accesseur, ct);

        await _repoEvenement.AjouterAsync(new EvenementInstance
        {
            IdInstance = idInstance,
            TypeEvenement = TypeEvenement.SignalRecu,
            IdNoeud = noeudId,
            NomNoeud = noeud.Nom,
            Horodatage = DateTime.UtcNow,
            Resultat = ResultatEvenement.Succes,
            Detail = $"Signal reçu : '{nomSignal}'"
        }, ct);

        var resultat = await _executeurAttenteSignal.ReprendreAsync(noeud, idInstance, ct);
        await ContinuerApresReprise(resultat, instance, definition, noeudId, accesseur, contexte, ct);
    }

    private async Task ContinuerApresReprise(
        ResultatNoeud resultatReprise,
        InstanceProcessus instance,
        DefinitionProcessus definition,
        string noeudActuelId,
        AccesseurVariables accesseur,
        ContexteExecution contexte,
        CancellationToken ct)
    {
        switch (resultatReprise.Type)
        {
            case TypeResultatNoeud.Suivant when resultatReprise.NoeudSuivantId is not null:
                await _repoInstance.MettreAJourStatutAsync(
                    instance.Id, StatutInstance.Active, null, null, ct);
                instance.Statut = StatutInstance.Active;
                instance.IdNoeudCourant = null;

                await _moteur.ReprendreDepuisAsync(
                    definition, instance, resultatReprise.NoeudSuivantId, contexte, ct);
                break;

            case TypeResultatNoeud.Termine:
                await _repoVariable.SauvegarderToutesAsync(
                    instance.Id, accesseur.ObtenirToutes(), ct);
                await _repoInstance.MettreAJourStatutAsync(
                    instance.Id, StatutInstance.Terminee, noeudActuelId, DateTime.UtcNow, ct);

                await _repoEvenement.AjouterAsync(new EvenementInstance
                {
                    IdInstance = instance.Id,
                    TypeEvenement = TypeEvenement.FinProcessus,
                    IdNoeud = noeudActuelId,
                    Horodatage = DateTime.UtcNow,
                    Resultat = ResultatEvenement.Succes
                }, ct);
                break;

            case TypeResultatNoeud.Suspendu:
                break;
        }
    }

    private async Task<InstanceProcessus> ObtenirInstanceValideAsync(
        long idInstance, StatutInstance statutAttendu, CancellationToken ct)
    {
        var instance = await _repoInstance.ObtenirParIdAsync(idInstance, ct)
            ?? throw new KeyNotFoundException($"Instance {idInstance} introuvable.");

        if (instance.Statut != statutAttendu)
            throw new EtatInstanceInvalideException(idInstance, instance.Statut,
                $"L'instance {idInstance} doit être '{statutAttendu}' mais est '{instance.Statut}'.");

        return instance;
    }

    private async Task<DefinitionProcessus> ChargerDefinitionInstanceAsync(
        InstanceProcessus instance, CancellationToken ct)
        => await _repoDefinition.ObtenirVersionPublieeAsync(
            instance.CleDefinition, instance.VersionDefinition, ct)
            ?? throw new DefinitionIntrouvableException(instance.CleDefinition, instance.VersionDefinition);

    private async Task<long?> ExtraireIdTacheExterneAsync(long idInstance, CancellationToken ct)
    {
        var derniereSuspension = await _repoEvenement.ObtenirDernierSuspensionAsync(idInstance, ct);
        if (derniereSuspension?.Detail is null) return null;

        try
        {
            using var doc = JsonDocument.Parse(derniereSuspension.Detail);
            if (doc.RootElement.TryGetProperty("idTacheExterne", out var el))
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var id))
                    return id;
            }
        }
        catch (JsonException) { }

        return null;
    }

    private static string SerialiserValeur(object valeur)
        => valeur switch
        {
            string s => s,
            DateTime dt => dt.ToString("O"),
            bool b => b.ToString().ToLowerInvariant(),
            _ => valeur.ToString() ?? string.Empty
        };
}
