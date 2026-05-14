using System.Data;
using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Oracle.Repositories;

public class RepositoryAttenteSignalOracle : OracleRepositoryBase, IRepositoryAttenteSignal
{
    public RepositoryAttenteSignalOracle(IDbConnection connection, string prefixe, IDbTransaction? tx = null) : base(connection, prefixe, tx) { }

    public Task CreerTablesAsync(IDbConnection connection) => Task.CompletedTask;

    public async Task AjouterAsync(long idInstance, string nomSignal, CancellationToken ct = default)
    {
        await Cn.ExecuteAsync(OraParam($"""
            INSERT INTO {T("ATTEN_SIGNL")} (NO_SEQ_ATTEN_SIGNL, NO_SEQ_INSTC_PROCS, NOM_SIGNL, DH_CREA)
            VALUES ({T("SEQ_SIGNL")}.NEXTVAL, :IdInstance, :NomSignal, :DateCreation)
            """),
            new { IdInstance = idInstance, NomSignal = nomSignal, DateCreation = DateTime.UtcNow },
            Tx);
    }

    public async Task SupprimerParInstanceAsync(long idInstance, CancellationToken ct = default)
    {
        await Cn.ExecuteAsync(OraParam($"""
            DELETE FROM {T("ATTEN_SIGNL")} WHERE NO_SEQ_INSTC_PROCS = :IdInstance
            """), new { IdInstance = idInstance });
    }

    public async Task<IReadOnlyList<long>> ObtenirInstancesEnAttenteAsync(
        string nomSignal, CancellationToken ct = default)
    {
        var ids = await Cn.QueryAsync<long>(OraParam($"""
            SELECT NO_SEQ_INSTC_PROCS FROM {T("ATTEN_SIGNL")} WHERE NOM_SIGNL = :NomSignal
            """), new { NomSignal = nomSignal });
        return ids.ToList();
    }

    public async Task<IReadOnlyList<string>> ObtenirSignauxParInstanceAsync(
        long idInstance, CancellationToken ct = default)
    {
        var signaux = await Cn.QueryAsync<string>(OraParam($"""
            SELECT NOM_SIGNL FROM {T("ATTEN_SIGNL")} WHERE NO_SEQ_INSTC_PROCS = :IdInstance
            """), new { IdInstance = idInstance });
        return signaux.ToList();
    }
}
