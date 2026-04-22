using System.Data;
using BpmPlus.Abstractions;

namespace BpmPlus.Core.Persistance;

public interface IRepositoryEvenement
{
    Task AjouterAsync(EvenementInstance evenement, CancellationToken ct = default);
    Task<IReadOnlyList<EvenementInstance>> ObtenirParInstanceAsync(long idInstance, CancellationToken ct = default);
    Task<EvenementInstance?> ObtenirDernierSuspensionAsync(long idInstance, CancellationToken ct = default);
    Task<EvenementInstance?> ObtenirDernierParTypeAsync(long idInstance, TypeEvenement type, CancellationToken ct = default);
    Task CreerTablesAsync(IDbConnection connection);
}
