using System.Data;
using BpmPlus.Core.Persistance;
using Dapper;

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
    private readonly string _prefixe;

    public SchemaCreator(
        IRepositoryDefinition repoDefinition,
        IRepositoryInstance repoInstance,
        IRepositoryVariable repoVariable,
        IRepositoryEvenement repoEvenement,
        IRepositoryAttenteSignal repoSignal,
        string prefixe)
    {
        _repoDefinition = repoDefinition;
        _repoInstance = repoInstance;
        _repoVariable = repoVariable;
        _repoEvenement = repoEvenement;
        _repoSignal = repoSignal;
        _prefixe = prefixe.TrimEnd('_').ToUpperInvariant();
    }

    public async Task CreerToutesLesTablesAsync(IDbConnection connection)
    {
        await _repoDefinition.CreerTablesAsync(connection);
        await _repoInstance.CreerTablesAsync(connection);
        await _repoVariable.CreerTablesAsync(connection);
        await _repoEvenement.CreerTablesAsync(connection);
        await _repoSignal.CreerTablesAsync(connection);
    }

    public async Task SupprimerEtRecreerToutesLesTablesAsync(IDbConnection connection)
    {
        var p = _prefixe;
        await connection.ExecuteAsync($"""
            DROP INDEX  IF EXISTS IDX_{p}_SIGNAL_NOM;
            DROP TABLE  IF EXISTS {p}_ATTENTE_SIGNAL;
            DROP TABLE  IF EXISTS {p}_EVENEMENT_INSTANCE;
            DROP TABLE  IF EXISTS {p}_VARIABLE_PROCESSUS;
            DROP INDEX  IF EXISTS IDX_{p}_INST_AGGID;
            DROP TABLE  IF EXISTS {p}_INSTANCE_PROCESSUS;
            DROP TABLE  IF EXISTS {p}_DEFINITION_PROCESSUS;
            """);
        await CreerToutesLesTablesAsync(connection);
    }
}
