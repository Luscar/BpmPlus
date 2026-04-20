using BpmPlus.Abstractions;

namespace BpmPlus.Tests.Integration.Infrastructure;

/// <summary>
/// Implémentation de IGestionTache pour les tests : enregistre les tâches créées et fermées.
/// </summary>
public class GestionTacheEnregistreur : IGestionTache
{
    private readonly List<(string Id, long IdInstance, string Titre)> _tachesCreees = new();
    private readonly List<string> _tachesFermees = new();

    public IReadOnlyList<(string Id, long IdInstance, string Titre)> TachesCreees => _tachesCreees;
    public IReadOnlyList<string> TachesFermees => _tachesFermees;

    public Task<string> CreerTacheAsync(
        DefinitionTache definitionTache,
        InstanceProcessus instance,
        CancellationToken ct = default)
    {
        var idTache = $"TACHE-{instance.Id}-{Guid.NewGuid():N}";
        _tachesCreees.Add((idTache, instance.Id, definitionTache.Titre ?? string.Empty));
        return Task.FromResult(idTache);
    }

    public Task FermerTacheAsync(string idTacheExterne, CancellationToken ct = default)
    {
        _tachesFermees.Add(idTacheExterne);
        return Task.CompletedTask;
    }

    public Task AssignerTacheAsync(string idTacheExterne, string assignee, CancellationToken ct = default)
        => Task.CompletedTask;
}
