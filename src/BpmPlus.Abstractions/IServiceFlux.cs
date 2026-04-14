using System.Data;

namespace BpmPlus.Abstractions;

/// <summary>
/// Point d'entrée principal pour interagir avec le moteur BPM.
/// Toutes les méthodes requièrent un IDbTransaction fourni par l'application cliente.
/// </summary>
public interface IServiceFlux
{
    // ── Instances ─────────────────────────────────────────────────────────────

    Task<long> DemarrerAsync(
        string cleDefinition,
        long aggregateId,
        IReadOnlyDictionary<string, object?>? variablesInitiales,
        IDbTransaction transaction,
        CancellationToken ct = default);

    Task<InstanceProcessus> ObtenirAsync(
        long idInstance,
        IDbTransaction transaction,
        CancellationToken ct = default);

    Task<InstanceProcessus?> ObtenirParAggregateAsync(
        string cleDefinition,
        long aggregateId,
        IDbTransaction transaction,
        CancellationToken ct = default);

    Task<IReadOnlyList<InstanceProcessus>> RechercherParVariableAsync(
        string nomVariable,
        object valeur,
        IDbTransaction transaction,
        CancellationToken ct = default);

    Task<IReadOnlyList<InstanceProcessus>> ObtenirEnfantsAsync(
        long idInstanceParent,
        IDbTransaction transaction,
        CancellationToken ct = default);

    // ── Étapes et reprise ─────────────────────────────────────────────────────

    Task TerminerEtapeAsync(
        long idInstance,
        IDbTransaction transaction,
        CancellationToken ct = default);

    Task EnvoyerSignalAsync(
        string nomSignal,
        IDbTransaction transaction,
        long? idInstance = null,
        CancellationToken ct = default);

    Task ReprendreAttenteTempsAsync(
        long idInstance,
        IDbTransaction transaction,
        CancellationToken ct = default);

    Task<IReadOnlyList<InstanceEchue>> ObtenirInstancesEchuesAsync(
        DateTime dateReference,
        IDbTransaction transaction,
        CancellationToken ct = default);

    // ── Variables ─────────────────────────────────────────────────────────────

    Task ModifierVariableAsync(
        long idInstance,
        string nomVariable,
        object? valeur,
        IDbTransaction transaction,
        CancellationToken ct = default);

    // ── Signaux en attente ────────────────────────────────────────────────────

    Task<IReadOnlyList<string>> ObtenirSignauxEnAttenteAsync(
        long idInstance,
        IDbTransaction transaction,
        CancellationToken ct = default);

    // ── Définitions ───────────────────────────────────────────────────────────

    Task SauvegarderDefinitionAsync(
        DefinitionProcessus definition,
        IDbTransaction transaction,
        CancellationToken ct = default);

    Task PublierDefinitionAsync(
        string cleDefinition,
        IDbTransaction transaction,
        CancellationToken ct = default);

    Task<IReadOnlyList<DefinitionProcessus>> ObtenirDefinitionsAsync(
        IDbTransaction transaction,
        CancellationToken ct = default);

    // ── Historique ────────────────────────────────────────────────────────────

    Task<IReadOnlyList<EvenementInstance>> ObtenirHistoriqueAsync(
        long idInstance,
        IDbTransaction transaction,
        CancellationToken ct = default);
}
