using System.Data;
using BpmPlus.Abstractions;

namespace BpmPlus.Core.Persistance;

public interface IRepositoryDefinition
{
    Task<long> SauvegarderAsync(DefinitionProcessus definition, IDbTransaction transaction, CancellationToken ct = default);
    Task<DefinitionProcessus?> ObtenirBrouillonAsync(string cle, IDbTransaction transaction, CancellationToken ct = default);
    Task<DefinitionProcessus?> ObtenirVersionPublieeAsync(string cle, int version, IDbTransaction transaction, CancellationToken ct = default);
    Task<DefinitionProcessus?> ObtenirDerniereVersionPublieeAsync(string cle, IDbTransaction transaction, CancellationToken ct = default);
    Task PublierAsync(string cle, IDbTransaction transaction, CancellationToken ct = default);
    Task<IReadOnlyList<DefinitionProcessus>> ObtenirToutesAsync(IDbTransaction transaction, CancellationToken ct = default);
    Task CreerTablesAsync(IDbConnection connection);
}
