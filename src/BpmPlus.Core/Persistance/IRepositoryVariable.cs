using System.Data;

namespace BpmPlus.Core.Persistance;

public interface IRepositoryVariable
{
    Task SauvegarderToutesAsync(long idInstance, IReadOnlyDictionary<string, object?> variables, IDbTransaction transaction, CancellationToken ct = default);
    Task<Dictionary<string, object?>> ChargerToutesAsync(long idInstance, IDbTransaction transaction, CancellationToken ct = default);
    Task MettreAJourAsync(long idInstance, string nom, object? valeur, IDbTransaction transaction, CancellationToken ct = default);
    Task CreerTablesAsync(IDbConnection connection);
}
