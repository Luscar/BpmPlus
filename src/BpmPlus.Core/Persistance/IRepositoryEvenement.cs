using System.Data;
using BpmPlus.Abstractions;

namespace BpmPlus.Core.Persistance;

public interface IRepositoryEvenement
{
    Task AjouterAsync(EvenementInstance evenement, IDbTransaction transaction, CancellationToken ct = default);
    Task<IReadOnlyList<EvenementInstance>> ObtenirParInstanceAsync(long idInstance, IDbTransaction transaction, CancellationToken ct = default);
    Task<EvenementInstance?> ObtenirDernierSuspensionAsync(long idInstance, IDbTransaction transaction, CancellationToken ct = default);
    Task CreerTablesAsync(IDbConnection connection);
}
