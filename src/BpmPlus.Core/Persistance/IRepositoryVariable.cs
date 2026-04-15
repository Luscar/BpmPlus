using System.Data;

namespace BpmPlus.Core.Persistance;

public interface IRepositoryVariable
{
    Task SauvegarderToutesAsync(long idInstance, IReadOnlyDictionary<string, object?> variables, CancellationToken ct = default);
    Task<Dictionary<string, object?>> ChargerToutesAsync(long idInstance, CancellationToken ct = default);
    Task MettreAJourAsync(long idInstance, string nom, object? valeur, CancellationToken ct = default);
    Task CreerTablesAsync(IDbConnection connection);
}
