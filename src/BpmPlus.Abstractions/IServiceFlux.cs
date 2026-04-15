namespace BpmPlus.Abstractions;

/// <summary>
/// Point d'entrée principal pour interagir avec le moteur BPM.
/// La session de base de données est fournie via IDbSession enregistré dans le conteneur IoC.
/// </summary>
public interface IServiceFlux
{
    // ── Instances ─────────────────────────────────────────────────────────────

    Task<long> DemarrerAsync(
        string cleDefinition,
        long aggregateId,
        IReadOnlyDictionary<string, object?>? variablesInitiales,
        CancellationToken ct = default);

    Task<InstanceProcessus> ObtenirAsync(
        long idInstance,
        CancellationToken ct = default);

    Task<InstanceProcessus?> ObtenirParAggregateAsync(
        string cleDefinition,
        long aggregateId,
        CancellationToken ct = default);

    Task<IReadOnlyList<InstanceProcessus>> RechercherParVariableAsync(
        string nomVariable,
        object valeur,
        CancellationToken ct = default);

    Task<IReadOnlyList<InstanceProcessus>> ObtenirEnfantsAsync(
        long idInstanceParent,
        CancellationToken ct = default);

    // ── Étapes et reprise ─────────────────────────────────────────────────────

    Task TerminerEtapeAsync(
        long idInstance,
        CancellationToken ct = default);

    Task EnvoyerSignalAsync(
        string nomSignal,
        long? idInstance = null,
        CancellationToken ct = default);

    Task ReprendreAttenteTempsAsync(
        long idInstance,
        CancellationToken ct = default);

    Task<IReadOnlyList<InstanceEchue>> ObtenirInstancesEchuesAsync(
        DateTime dateReference,
        CancellationToken ct = default);

    // ── Variables ─────────────────────────────────────────────────────────────

    Task ModifierVariableAsync(
        long idInstance,
        string nomVariable,
        object? valeur,
        CancellationToken ct = default);

    // ── Signaux en attente ────────────────────────────────────────────────────

    Task<IReadOnlyList<string>> ObtenirSignauxEnAttenteAsync(
        long idInstance,
        CancellationToken ct = default);

    // ── Définitions ───────────────────────────────────────────────────────────

    Task SauvegarderDefinitionAsync(
        DefinitionProcessus definition,
        CancellationToken ct = default);

    Task PublierDefinitionAsync(
        string cleDefinition,
        CancellationToken ct = default);

    Task<IReadOnlyList<DefinitionProcessus>> ObtenirDefinitionsAsync(
        CancellationToken ct = default);

    // ── Historique ────────────────────────────────────────────────────────────

    Task<IReadOnlyList<EvenementInstance>> ObtenirHistoriqueAsync(
        long idInstance,
        CancellationToken ct = default);
}
