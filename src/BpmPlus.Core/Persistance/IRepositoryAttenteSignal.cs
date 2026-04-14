using System.Data;

namespace BpmPlus.Core.Persistance;

public interface IRepositoryAttenteSignal
{
    Task AjouterAsync(long idInstance, string nomSignal, IDbTransaction transaction, CancellationToken ct = default);
    Task SupprimerParInstanceAsync(long idInstance, IDbTransaction transaction, CancellationToken ct = default);
    Task<IReadOnlyList<long>> ObtenirInstancesEnAttenteAsync(string nomSignal, IDbTransaction transaction, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ObtenirSignauxParInstanceAsync(long idInstance, IDbTransaction transaction, CancellationToken ct = default);
    Task CreerTablesAsync(IDbConnection connection);
}
