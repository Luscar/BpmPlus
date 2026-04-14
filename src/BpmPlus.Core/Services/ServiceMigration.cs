using System.Data;
using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Microsoft.Extensions.Logging;

namespace BpmPlus.Core.Services;

public class ServiceMigration : IServiceMigration
{
    private readonly IRepositoryInstance _repoInstance;
    private readonly IRepositoryDefinition _repoDefinition;
    private readonly IRepositoryEvenement _repoEvenement;
    private readonly ILogger<ServiceMigration> _logger;

    public ServiceMigration(
        IRepositoryInstance repoInstance,
        IRepositoryDefinition repoDefinition,
        IRepositoryEvenement repoEvenement,
        ILogger<ServiceMigration> logger)
    {
        _repoInstance = repoInstance;
        _repoDefinition = repoDefinition;
        _repoEvenement = repoEvenement;
        _logger = logger;
    }

    public async Task<ResultatMigration> MigrerAsync(
        long idInstance,
        int versionCible,
        IDbTransaction transaction,
        IReadOnlyDictionary<string, string>? mappingNoeuds = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Migration instance {Id} → version {Version}", idInstance, versionCible);

        var instance = await _repoInstance.ObtenirParIdAsync(idInstance, transaction, ct)
            ?? throw new KeyNotFoundException($"Instance {idInstance} introuvable.");

        if (instance.Statut != StatutInstance.Active && instance.Statut != StatutInstance.Suspendue)
        {
            var msg = $"L'instance {idInstance} ne peut pas être migrée (statut : {instance.Statut}).";
            _logger.LogError(msg);
            return new ResultatMigration(
                idInstance, false, instance.VersionDefinition, versionCible,
                instance.IdNoeudCourant, null, msg);
        }

        DefinitionProcessus definitionCible;
        try
        {
            definitionCible = await _repoDefinition.ObtenirVersionPublieeAsync(
                instance.CleDefinition, versionCible, transaction, ct)
                ?? throw new DefinitionIntrouvableException(instance.CleDefinition, versionCible);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Définition cible introuvable pour la migration");
            return new ResultatMigration(
                idInstance, false, instance.VersionDefinition, versionCible,
                instance.IdNoeudCourant, null, ex.Message);
        }

        // Résoudre le nœud courant dans la nouvelle version
        var ancienNoeudId = instance.IdNoeudCourant;
        string? nouveauNoeudId = ancienNoeudId;

        if (ancienNoeudId is not null)
        {
            if (definitionCible.TrouverNoeud(ancienNoeudId) is not null)
            {
                // Le nœud existe tel quel dans la nouvelle version
                nouveauNoeudId = ancienNoeudId;
            }
            else if (mappingNoeuds is not null && mappingNoeuds.TryGetValue(ancienNoeudId, out var mapped))
            {
                if (definitionCible.TrouverNoeud(mapped) is null)
                {
                    var msg = $"Le nœud mappé '{mapped}' n'existe pas dans la définition cible v{versionCible}.";
                    _logger.LogError(msg);
                    return new ResultatMigration(
                        idInstance, false, instance.VersionDefinition, versionCible,
                        ancienNoeudId, null, msg);
                }
                nouveauNoeudId = mapped;
            }
            else
            {
                var msg = $"Le nœud '{ancienNoeudId}' n'existe pas dans la définition cible v{versionCible} " +
                          "et aucun mapping n'est fourni.";
                _logger.LogError(msg);
                throw new MigrationImpossibleException(idInstance, ancienNoeudId, msg);
            }
        }

        // Mettre à jour l'instance
        await _repoInstance.MettreAJourVersionAsync(idInstance, versionCible, nouveauNoeudId, transaction, ct);

        // Enregistrer l'événement de migration
        var detail = System.Text.Json.JsonSerializer.Serialize(new
        {
            ancienneVersion = instance.VersionDefinition,
            nouvelleVersion = versionCible,
            ancienNoeud = ancienNoeudId,
            nouveauNoeud = nouveauNoeudId
        });

        await _repoEvenement.AjouterAsync(new EvenementInstance
        {
            IdInstance = idInstance,
            TypeEvenement = TypeEvenement.MigrationInstance,
            IdNoeud = nouveauNoeudId,
            Horodatage = DateTime.UtcNow,
            Resultat = ResultatEvenement.Succes,
            Detail = detail
        }, transaction, ct);

        _logger.LogInformation(
            "Migration réussie — instance {Id} : v{AncV}@{AncN} → v{NvV}@{NvN}",
            idInstance, instance.VersionDefinition, ancienNoeudId, versionCible, nouveauNoeudId);

        return new ResultatMigration(
            idInstance, true, instance.VersionDefinition, versionCible,
            ancienNoeudId, nouveauNoeudId, null);
    }

    public async Task<IReadOnlyList<ResultatMigration>> MigrerToutesAsync(
        string cleDefinition,
        int versionCible,
        IDbTransaction transaction,
        IReadOnlyDictionary<string, string>? mappingNoeuds = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Migration de toutes les instances '{Cle}' → version {Version}",
            cleDefinition, versionCible);

        // Récupérer toutes les instances actives/suspendues pour cette définition
        var suspendues = await _repoInstance.ObtenirSuspenduesAsync(transaction, ct);
        var instances = suspendues.Where(i =>
            i.CleDefinition == cleDefinition &&
            (i.Statut == StatutInstance.Active || i.Statut == StatutInstance.Suspendue))
            .ToList();

        var resultats = new List<ResultatMigration>();
        foreach (var instance in instances)
        {
            try
            {
                var resultat = await MigrerAsync(instance.Id, versionCible, transaction, mappingNoeuds, ct);
                resultats.Add(resultat);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la migration de l'instance {Id}", instance.Id);
                resultats.Add(new ResultatMigration(
                    instance.Id, false, instance.VersionDefinition, versionCible,
                    instance.IdNoeudCourant, null, ex.Message));
            }
        }

        return resultats;
    }
}
