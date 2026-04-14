using System.Data;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Oracle.Repositories;

public class RepositoryAttenteSignalOracle : OracleRepositoryBase, IRepositoryAttenteSignal
{
    public RepositoryAttenteSignalOracle(string prefixe) : base(prefixe) { }

    public Task CreerTablesAsync(IDbConnection connection) => Task.CompletedTask;

    public async Task AjouterAsync(
        long idInstance, string nomSignal,
        IDbTransaction transaction, CancellationToken ct = default)
    {
        await Cn(transaction).ExecuteAsync(OraParam($"""
            INSERT INTO {T("ATTENTE_SIGNAL")} (ID, ID_INSTANCE, NOM_SIGNAL, DATE_CREATION)
            VALUES ({T("SEQ_SIGNAL")}.NEXTVAL, :IdInstance, :NomSignal, :DateCreation)
            """),
            new { IdInstance = idInstance, NomSignal = nomSignal, DateCreation = DateTime.UtcNow },
            transaction);
    }

    public async Task SupprimerParInstanceAsync(
        long idInstance, IDbTransaction transaction, CancellationToken ct = default)
    {
        await Cn(transaction).ExecuteAsync(OraParam($"""
            DELETE FROM {T("ATTENTE_SIGNAL")} WHERE ID_INSTANCE = :IdInstance
            """), new { IdInstance = idInstance }, transaction);
    }

    public async Task<IReadOnlyList<long>> ObtenirInstancesEnAttenteAsync(
        string nomSignal, IDbTransaction transaction, CancellationToken ct = default)
    {
        var ids = await Cn(transaction).QueryAsync<long>(OraParam($"""
            SELECT ID_INSTANCE FROM {T("ATTENTE_SIGNAL")} WHERE NOM_SIGNAL = :NomSignal
            """), new { NomSignal = nomSignal }, transaction);
        return ids.ToList();
    }

    public async Task<IReadOnlyList<string>> ObtenirSignauxParInstanceAsync(
        long idInstance, IDbTransaction transaction, CancellationToken ct = default)
    {
        var signaux = await Cn(transaction).QueryAsync<string>(OraParam($"""
            SELECT NOM_SIGNAL FROM {T("ATTENTE_SIGNAL")} WHERE ID_INSTANCE = :IdInstance
            """), new { IdInstance = idInstance }, transaction);
        return signaux.ToList();
    }
}
