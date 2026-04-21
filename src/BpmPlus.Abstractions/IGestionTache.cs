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
    /// </summary>
    Task<long> CreerTacheAsync(
        DefinitionTache definitionTache,
        InstanceProcessus instance,
        CancellationToken ct = default);

    /// <summary>
    /// Ferme la tâche externe lors de la complétion d'un NoeudInteractif.
    /// Appelé dans la même transaction que la reprise de l'instance.
    /// </summary>
    Task FermerTacheAsync(long idTacheExterne, CancellationToken ct = default);

    /// <summary>
    /// Assigne la tâche à un utilisateur ou groupe.
    /// </summary>
    Task AssignerTacheAsync(long idTacheExterne, string assignee, CancellationToken ct = default);
}
