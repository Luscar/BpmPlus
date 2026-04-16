using System.Data;
using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Oracle.Repositories;

public class RepositoryAttenteSignalOracle : OracleRepositoryBase, IRepositoryAttenteSignal
{
    public RepositoryAttenteSignalOracle(IDbConnection connection, string prefixe) : base(connection, prefixe) { }

    public Task CreerTablesAsync(IDbConnection connection) => Task.CompletedTask;

    public async Task AjouterAsync(long idInstance, string nomSignal, CancellationToken ct = default)
    {
        await Cn.ExecuteAsync(OraParam($"""
            INSERT INTO {T("ATTENTE_SIGNAL")} (ID, ID_INSTANCE, NOM_SIGNAL, DATE_CREATION)
            VALUES ({T("SEQ_SIGNAL")}.NEXTVAL, :IdInstance, :NomSignal, :DateCreation)
            """),
            new { IdInstance = idInstance, NomSignal = nomSignal, DateCreation = DateTime.UtcNow },
            Tx);
    }

    public async Task SupprimerParInstanceAsync(long idInstance, CancellationToken ct = default)
    {
        await Cn.ExecuteAsync(OraParam($"""
            DELETE FROM {T("ATTENTE_SIGNAL")} WHERE ID_INSTANCE = :IdInstance
            """), new { IdInstance = idInstance });
    }

    public async Task<IReadOnlyList<long>> ObtenirInstancesEnAttenteAsync(
        string nomSignal, CancellationToken ct = default)
    {
        var ids = await Cn.QueryAsync<long>(OraParam($"""
            SELECT ID_INSTANCE FROM {T("ATTENTE_SIGNAL")} WHERE NOM_SIGNAL = :NomSignal
            """), new { NomSignal = nomSignal });
        return ids.ToList();
    }

    public async Task<IReadOnlyList<string>> ObtenirSignauxParInstanceAsync(
        long idInstance, CancellationToken ct = default)
    {
        var signaux = await Cn.QueryAsync<string>(OraParam($"""
            SELECT NOM_SIGNAL FROM {T("ATTENTE_SIGNAL")} WHERE ID_INSTANCE = :IdInstance
            """), new { IdInstance = idInstance });
        return signaux.ToList();
    }
}
