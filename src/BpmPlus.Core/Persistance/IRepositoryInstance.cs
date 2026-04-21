using System.Data;
using BpmPlus.Abstractions;

namespace BpmPlus.Core.Persistance;

public interface IRepositoryInstance
{
    Task<long> CreerAsync(InstanceProcessus instance, CancellationToken ct = default);
    Task<InstanceProcessus?> ObtenirParIdAsync(long id, CancellationToken ct = default);
    Task<InstanceProcessus?> ObtenirActiveParAggregateAsync(string cleDefinition, long aggregateId, CancellationToken ct = default);
    Task<IReadOnlyList<InstanceProcessus>> ObtenirEnfantsAsync(long idParent, CancellationToken ct = default);
    Task<IReadOnlyList<InstanceProcessus>> RechercherParVariableAsync(string nomVariable, string valeurSerialisee, CancellationToken ct = default);
    Task<IReadOnlyList<InstanceProcessus>> RechercherParVariableAsync(string nomVariable, string valeurSerialisee, StatutInstance statut, CancellationToken ct = default);
    Task<IReadOnlyList<InstanceProcessus>> ObtenirSuspenduesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InstanceProcessus>> ObtenirParStatutAsync(StatutInstance statut, CancellationToken ct = default);
    Task MettreAJourStatutAsync(long id, StatutInstance statut, string? idNoeudCourant, DateTime? dateFin, CancellationToken ct = default);
    Task MettreAJourVersionAsync(long id, int nouvelleVersion, string? idNoeudCourant, CancellationToken ct = default);
    Task<bool> ExisteProcessusActifAsync(string cleDefinition, long aggregateId, CancellationToken ct = default);
    Task CreerTablesAsync(IDbConnection connection);
}
