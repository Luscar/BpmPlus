namespace BpmPlus.Abstractions;

/// <summary>
/// Service de migration manuelle d'instances vers une nouvelle version de définition.
/// La migration met à jour l'instance en place (l'ID est conservé).
/// La session de base de données est fournie via IDbSession enregistré dans le conteneur IoC.
/// </summary>
public interface IServiceMigration
{
    Task<ResultatMigration> MigrerAsync(
        long idInstance,
        int versionCible,
        IReadOnlyDictionary<string, string>? mappingNoeuds = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<ResultatMigration>> MigrerToutesAsync(
        string cleDefinition,
        int versionCible,
        IReadOnlyDictionary<string, string>? mappingNoeuds = null,
        CancellationToken ct = default);
}
