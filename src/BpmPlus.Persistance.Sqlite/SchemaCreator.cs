using System.Data;
using BpmPlus.Core.Persistance;

namespace BpmPlus.Persistance.Sqlite;

/// <summary>
/// Crée les tables nécessaires dans SQLite (utilisé pour les tests d'intégration).
/// </summary>
public class SchemaCreator
{
    private readonly IRepositoryDefinition _repoDefinition;
    private readonly IRepositoryInstance _repoInstance;
    private readonly IRepositoryVariable _repoVariable;
    private readonly IRepositoryEvenement _repoEvenement;
    private readonly IRepositoryAttenteSignal _repoSignal;

    public SchemaCreator(
        IRepositoryDefinition repoDefinition,
        IRepositoryInstance repoInstance,
        IRepositoryVariable repoVariable,
        IRepositoryEvenement repoEvenement,
        IRepositoryAttenteSignal repoSignal)
    {
        _repoDefinition = repoDefinition;
        _repoInstance = repoInstance;
        _repoVariable = repoVariable;
        _repoEvenement = repoEvenement;
        _repoSignal = repoSignal;
    }

    public async Task CreerToutesLesTablesAsync(IDbConnection connection)
    {
        await _repoDefinition.CreerTablesAsync(connection);
        await _repoInstance.CreerTablesAsync(connection);
        await _repoVariable.CreerTablesAsync(connection);
        await _repoEvenement.CreerTablesAsync(connection);
        await _repoSignal.CreerTablesAsync(connection);
    }
}
