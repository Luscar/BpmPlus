using BpmPlus.Abstractions;
using BpmPlus.Core.Execution.Executeurs;
using BpmPlus.Core.Persistance;
using Microsoft.Extensions.Logging;

namespace BpmPlus.Core.Execution;

public enum TypeResultatExecution { Suspendu, Termine }

public record ResultatExecution(TypeResultatExecution Type, string? IdDernierNoeud, string? Detail);

/// <summary>
/// Moteur d'exécution principal. Enchaîne les nœuds en mémoire jusqu'à une suspension ou une fin.
/// </summary>
public class MoteurExecution
{
    private readonly ExecuteurNoeudMetier _executeurMetier;
    private readonly ExecuteurNoeudInteractif _executeurInteractif;
    private readonly ExecuteurNoeudDecision _executeurDecision;
    private readonly ExecuteurNoeudAttenteTemps _executeurAttenteTemps;
    private readonly ExecuteurNoeudAttenteSignal _executeurAttenteSignal;
    private readonly IRepositoryInstance _repoInstance;
    private readonly IRepositoryVariable _repoVariable;
    private readonly IRepositoryEvenement _repoEvenement;
    private readonly Lazy<ExecuteurNoeudSousProcessus> _executeurSousProcessus;
    private readonly ILogger<MoteurExecution> _logger;

    public MoteurExecution(
        ExecuteurNoeudMetier executeurMetier,
        ExecuteurNoeudInteractif executeurInteractif,
        ExecuteurNoeudDecision executeurDecision,
        ExecuteurNoeudAttenteTemps executeurAttenteTemps,
        ExecuteurNoeudAttenteSignal executeurAttenteSignal,
        Lazy<ExecuteurNoeudSousProcessus> executeurSousProcessus,
        IRepositoryInstance repoInstance,
        IRepositoryVariable repoVariable,
        IRepositoryEvenement repoEvenement,
        ILogger<MoteurExecution> logger)
    {
        _executeurMetier = executeurMetier;
        _executeurInteractif = executeurInteractif;
        _executeurDecision = executeurDecision;
        _executeurAttenteTemps = executeurAttenteTemps;
        _executeurAttenteSignal = executeurAttenteSignal;
        _executeurSousProcessus = executeurSousProcessus;
        _repoInstance = repoInstance;
        _repoVariable = repoVariable;
        _repoEvenement = repoEvenement;
        _logger = logger;
    }

    /// <summary>Démarre l'exécution depuis le nœud de début de la définition.</summary>
    public Task<TypeResultatExecution> ExecuterDepuisDebutAsync(
        DefinitionProcessus definition,
        InstanceProcessus instance,
        ContexteExecution contexte,
        CancellationToken ct)
        => ExecuterAsync(definition, instance, definition.NoeudDebutId, contexte, ct);

    /// <summary>Reprend l'exécution depuis un nœud spécifique (après suspension).</summary>
    public Task<TypeResultatExecution> ReprendreDepuisAsync(
        DefinitionProcessus definition,
        InstanceProcessus instance,
        string noeudId,
        ContexteExecution contexte,
        CancellationToken ct)
        => ExecuterAsync(definition, instance, noeudId, contexte, ct);

    private async Task<TypeResultatExecution> ExecuterAsync(
        DefinitionProcessus definition,
        InstanceProcessus instance,
        string noeudDebutId,
        ContexteExecution contexte,
        CancellationToken ct)
    {
        string? noeudCourantId = noeudDebutId;

        while (noeudCourantId is not null)
        {
            ct.ThrowIfCancellationRequested();

            var noeud = definition.TrouverNoeud(noeudCourantId)
                ?? throw new NoeudIntrouvableException(noeudCourantId);

            _logger.LogInformation("Instance {Id} — entrée nœud '{NoeudId}' ({Type})",
                instance.Id, noeud.Id, noeud.GetType().Name);

            var debut = DateTime.UtcNow;
            await EnregistrerEvenementAsync(
                instance.Id, TypeEvenement.EntreeNoeud, noeud, null, null, ct);

            ResultatNoeud resultat;
            try
            {
                resultat = await DispatcherNoeudAsync(noeud, instance, contexte, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Instance {Id} — erreur nœud '{NoeudId}'", instance.Id, noeud.Id);
                await EnregistrerEvenementAsync(
                    instance.Id, TypeEvenement.ErreurNoeud, noeud,
                    ResultatEvenement.Erreur, ex.Message, ct);
                throw;
            }

            var dureeMs = (long)(DateTime.UtcNow - debut).TotalMilliseconds;

            switch (resultat.Type)
            {
                case TypeResultatNoeud.Suivant:
                    await EnregistrerEvenementAsync(
                        instance.Id, TypeEvenement.SortieNoeud, noeud,
                        ResultatEvenement.Succes, null, ct, dureeMs);
                    noeudCourantId = resultat.NoeudSuivantId;
                    break;

                case TypeResultatNoeud.Suspendu:
                    await EnregistrerEvenementAsync(
                        instance.Id, TypeEvenement.NoeudSuspendu, noeud,
                        ResultatEvenement.Suspendu, resultat.Detail, ct, dureeMs);

                    await PersisterEtatAsync(instance, noeud.Id, StatutInstance.Suspendue,
                        null, contexte, ct);

                    _logger.LogInformation("Instance {Id} — suspendue sur nœud '{NoeudId}'",
                        instance.Id, noeud.Id);
                    return TypeResultatExecution.Suspendu;

                case TypeResultatNoeud.Termine:
                    await EnregistrerEvenementAsync(
                        instance.Id, TypeEvenement.SortieNoeud, noeud,
                        ResultatEvenement.Succes, null, ct, dureeMs);
                    await EnregistrerEvenementAsync(
                        instance.Id, TypeEvenement.FinProcessus, noeud,
                        ResultatEvenement.Succes, null, ct);

                    await PersisterEtatAsync(instance, noeud.Id, StatutInstance.Terminee,
                        DateTime.UtcNow, contexte, ct);

                    _logger.LogInformation("Instance {Id} — terminée sur nœud '{NoeudId}'",
                        instance.Id, noeud.Id);
                    return TypeResultatExecution.Termine;
            }
        }

        _logger.LogWarning("Instance {Id} — fin de flux sans nœud EstFinale.", instance.Id);
        await PersisterEtatAsync(instance, null, StatutInstance.Terminee, DateTime.UtcNow, contexte, ct);
        return TypeResultatExecution.Termine;
    }

    private async Task<ResultatNoeud> DispatcherNoeudAsync(
        NoeudProcessus noeud,
        InstanceProcessus instance,
        ContexteExecution contexte,
        CancellationToken ct)
    {
        return noeud switch
        {
            NoeudMetier nm => await _executeurMetier.ExecuterAsync(nm, contexte, ct),
            NoeudInteractif ni => await _executeurInteractif.EntrerAsync(ni, instance, contexte, ct),
            NoeudDecision nd => await _executeurDecision.ExecuterAsync(nd, contexte, ct),
            NoeudAttenteTemps nat => await _executeurAttenteTemps.EntrerAsync(nat, contexte, ct),
            NoeudAttenteSignal nas => await _executeurAttenteSignal.EntrerAsync(nas, instance.Id, ct),
            NoeudSousProcessus nsp => await _executeurSousProcessus.Value.ExecuterAsync(nsp, instance, contexte, ct),
            _ => throw new InvalidOperationException($"Type de nœud non supporté : {noeud.GetType().Name}")
        };
    }

    private async Task PersisterEtatAsync(
        InstanceProcessus instance,
        string? idNoeudCourant,
        StatutInstance statut,
        DateTime? dateFin,
        ContexteExecution contexte,
        CancellationToken ct)
    {
        if (contexte.Variables is AccesseurVariables acc && acc.EstModifie)
        {
            await _repoVariable.SauvegarderToutesAsync(
                instance.Id, contexte.Variables.ObtenirToutes(), ct);
        }

        await _repoInstance.MettreAJourStatutAsync(
            instance.Id, statut, idNoeudCourant, dateFin, ct);

        instance.Statut = statut;
        instance.IdNoeudCourant = idNoeudCourant;
        instance.DateFin = dateFin;
    }

    private async Task EnregistrerEvenementAsync(
        long idInstance,
        TypeEvenement type,
        NoeudProcessus noeud,
        ResultatEvenement? resultat,
        string? detail,
        CancellationToken ct,
        long? dureeMs = null)
    {
        var evt = new EvenementInstance
        {
            IdInstance = idInstance,
            TypeEvenement = type,
            IdNoeud = noeud.Id,
            NomNoeud = noeud.Nom,
            Horodatage = DateTime.UtcNow,
            DureeMs = dureeMs,
            Resultat = resultat,
            Detail = detail
        };
        await _repoEvenement.AjouterAsync(evt, ct);
    }

    public async Task EnregistrerEvenementSimpleAsync(
        long idInstance,
        TypeEvenement type,
        string? idNoeud,
        string? nomNoeud,
        ResultatEvenement? resultat,
        string? detail,
        CancellationToken ct)
    {
        var evt = new EvenementInstance
        {
            IdInstance = idInstance,
            TypeEvenement = type,
            IdNoeud = idNoeud,
            NomNoeud = nomNoeud,
            Horodatage = DateTime.UtcNow,
            Resultat = resultat,
            Detail = detail
        };
        await _repoEvenement.AjouterAsync(evt, ct);
    }
}
