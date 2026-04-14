using System.Data;
using BpmPlus.Abstractions;

namespace BpmPlus.Core.Persistance;

public interface IRepositoryInstance
{
    Task<long> CreerAsync(InstanceProcessus instance, IDbTransaction transaction, CancellationToken ct = default);
    Task<InstanceProcessus?> ObtenirParIdAsync(long id, IDbTransaction transaction, CancellationToken ct = default);
    Task<InstanceProcessus?> ObtenirActiveParAggregateAsync(string cleDefinition, long aggregateId, IDbTransaction transaction, CancellationToken ct = default);
    Task<IReadOnlyList<InstanceProcessus>> ObtenirEnfantsAsync(long idParent, IDbTransaction transaction, CancellationToken ct = default);
    Task<IReadOnlyList<InstanceProcessus>> RechercherParVariableAsync(string nomVariable, string valeurSerialisee, IDbTransaction transaction, CancellationToken ct = default);
    Task<IReadOnlyList<InstanceProcessus>> ObtenirSuspenduesAsync(IDbTransaction transaction, CancellationToken ct = default);
    Task MettreAJourStatutAsync(long id, StatutInstance statut, string? idNoeudCourant, DateTime? dateFin, IDbTransaction transaction, CancellationToken ct = default);
    Task MettreAJourVersionAsync(long id, int nouvelleVersion, string? idNoeudCourant, IDbTransaction transaction, CancellationToken ct = default);
    Task<bool> ExisteProcessusActifAsync(string cleDefinition, long aggregateId, IDbTransaction transaction, CancellationToken ct = default);
    Task CreerTablesAsync(IDbConnection connection);
}
