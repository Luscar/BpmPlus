using System.Data;

namespace BpmPlus.Core.Persistance;

public interface IRepositoryAttenteSignal
{
    Task AjouterAsync(long idInstance, string nomSignal, CancellationToken ct = default);
    Task SupprimerParInstanceAsync(long idInstance, CancellationToken ct = default);
    Task<IReadOnlyList<long>> ObtenirInstancesEnAttenteAsync(string nomSignal, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ObtenirSignauxParInstanceAsync(long idInstance, CancellationToken ct = default);
    Task CreerTablesAsync(IDbConnection connection);
}
