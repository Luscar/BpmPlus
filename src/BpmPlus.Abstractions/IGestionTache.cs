namespace BpmPlus.Abstractions;

/// <summary>
/// Gestionnaire de tâches humaines. Fait le lien entre le moteur BPM
/// et le système externe de gestion de tâches de l'application cliente.
/// </summary>
public interface IGestionTache
{
    /// <summary>
    /// Crée une tâche dans le système externe lors de l'arrivée sur un NoeudInteractif.
    /// Appelé dans la même transaction que la suspension de l'instance.
    /// L'identifiant du processus (<see cref="InstanceProcessus.Id"/>) sert de clé de corrélation.
    /// </summary>
    Task CreerTacheAsync(
        DefinitionTache definitionTache,
        InstanceProcessus instance,
        string? logonAuto = null,
        CancellationToken ct = default);

    /// <summary>
    /// Ferme la tâche externe lors de la complétion d'un NoeudInteractif.
    /// Appelé dans la même transaction que la reprise de l'instance.
    /// </summary>
    /// <param name="instance">Instance de processus au moment de la complétion (utiliser <see cref="InstanceProcessus.Id"/> pour identifier la tâche).</param>
    /// <param name="variables">Snapshot des variables de l'instance au moment de la complétion.</param>
    Task FermerTacheAsync(
        InstanceProcessus instance,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken ct = default);

    /// <summary>
    /// Assigne la tâche à un utilisateur ou groupe.
    /// </summary>
    /// <param name="idProcessus">Identifiant de l'instance de processus, utilisé comme clé de la tâche.</param>
    Task AssignerTacheAsync(long idProcessus, string assignee, CancellationToken ct = default);
}
