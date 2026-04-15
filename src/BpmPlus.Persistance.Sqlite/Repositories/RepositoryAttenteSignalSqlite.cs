using System.Data;
using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Sqlite.Repositories;

public class RepositoryAttenteSignalSqlite : SqliteRepositoryBase, IRepositoryAttenteSignal
{
    public RepositoryAttenteSignalSqlite(IDbSession session, string prefixe) : base(session, prefixe) { }

    public async Task CreerTablesAsync(IDbConnection connection)
    {
        await connection.ExecuteAsync($"""
            CREATE TABLE IF NOT EXISTS {T("ATTENTE_SIGNAL")} (
                ID           INTEGER PRIMARY KEY AUTOINCREMENT,
                ID_INSTANCE  INTEGER NOT NULL,
                NOM_SIGNAL   TEXT    NOT NULL,
                DATE_CREATION TEXT   NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IDX_{Prefixe}_SIGNAL_NOM
                ON {T("ATTENTE_SIGNAL")}(NOM_SIGNAL);
            """);
    }

    public async Task AjouterAsync(long idInstance, string nomSignal, CancellationToken ct = default)
    {
        await Cn.ExecuteAsync($"""
            INSERT INTO {T("ATTENTE_SIGNAL")} (ID_INSTANCE, NOM_SIGNAL, DATE_CREATION)
            VALUES (@IdInstance, @NomSignal, @DateCreation)
            """,
            new { IdInstance = idInstance, NomSignal = nomSignal, DateCreation = DateTime.UtcNow.ToString("O") },
            Tx);
    }

    public async Task SupprimerParInstanceAsync(long idInstance, CancellationToken ct = default)
    {
        await Cn.ExecuteAsync($"""
            DELETE FROM {T("ATTENTE_SIGNAL")} WHERE ID_INSTANCE = @IdInstance
            """, new { IdInstance = idInstance }, Tx);
    }

    public async Task<IReadOnlyList<long>> ObtenirInstancesEnAttenteAsync(
        string nomSignal, CancellationToken ct = default)
    {
        var ids = await Cn.QueryAsync<long>($"""
            SELECT ID_INSTANCE FROM {T("ATTENTE_SIGNAL")} WHERE NOM_SIGNAL = @NomSignal
            """, new { NomSignal = nomSignal }, Tx);
        return ids.ToList();
    }

    public async Task<IReadOnlyList<string>> ObtenirSignauxParInstanceAsync(
        long idInstance, CancellationToken ct = default)
    {
        var signaux = await Cn.QueryAsync<string>($"""
            SELECT NOM_SIGNAL FROM {T("ATTENTE_SIGNAL")} WHERE ID_INSTANCE = @IdInstance
            """, new { IdInstance = idInstance }, Tx);
        return signaux.ToList();
    }
}
