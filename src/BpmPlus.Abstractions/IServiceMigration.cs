using System.Data;

namespace BpmPlus.Abstractions;

/// <summary>
/// Service de migration manuelle d'instances vers une nouvelle version de définition.
/// La migration met à jour l'instance en place (l'ID est conservé).
/// </summary>
public interface IServiceMigration
{
    Task<ResultatMigration> MigrerAsync(
        long idInstance,
        int versionCible,
        IDbTransaction transaction,
        IReadOnlyDictionary<string, string>? mappingNoeuds = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<ResultatMigration>> MigrerToutesAsync(
        string cleDefinition,
        int versionCible,
        IDbTransaction transaction,
        IReadOnlyDictionary<string, string>? mappingNoeuds = null,
        CancellationToken ct = default);
}
