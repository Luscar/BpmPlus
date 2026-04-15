using System.Data;
using BpmPlus.Abstractions;

namespace BpmPlus.Core.Persistance;

public interface IRepositoryDefinition
{
    Task<long> SauvegarderAsync(DefinitionProcessus definition, CancellationToken ct = default);
    Task<DefinitionProcessus?> ObtenirBrouillonAsync(string cle, CancellationToken ct = default);
    Task<DefinitionProcessus?> ObtenirVersionPublieeAsync(string cle, int version, CancellationToken ct = default);
    Task<DefinitionProcessus?> ObtenirDerniereVersionPublieeAsync(string cle, CancellationToken ct = default);
    Task PublierAsync(string cle, CancellationToken ct = default);
    Task<IReadOnlyList<DefinitionProcessus>> ObtenirToutesAsync(CancellationToken ct = default);
    Task CreerTablesAsync(IDbConnection connection);
}
