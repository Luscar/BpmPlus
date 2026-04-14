using System.Data;
using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Sqlite.Repositories;

public class RepositoryInstanceSqlite : SqliteRepositoryBase, IRepositoryInstance
{
    public RepositoryInstanceSqlite(string prefixe) : base(prefixe) { }

    public async Task CreerTablesAsync(IDbConnection connection)
    {
        await connection.ExecuteAsync($"""
            CREATE TABLE IF NOT EXISTS {T("INSTANCE_PROCESSUS")} (
                ID                  INTEGER PRIMARY KEY AUTOINCREMENT,
                CLE_DEFINITION      TEXT    NOT NULL,
                VERSION_DEFINITION  INTEGER NOT NULL,
                AGGREGATE_ID        INTEGER NOT NULL,
                STATUT              TEXT    NOT NULL,
                ID_NOEUD_COURANT    TEXT    NULL,
                ID_INSTANCE_PARENT  INTEGER NULL,
                DATE_DEBUT          TEXT    NOT NULL,
                DATE_FIN            TEXT    NULL,
                DATE_CREATION       TEXT    NOT NULL,
                DATE_MAJ            TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IDX_{Prefixe}_INST_AGGID
                ON {T("INSTANCE_PROCESSUS")}(AGGREGATE_ID);
            """);
    }

    public async Task<long> CreerAsync(
        InstanceProcessus instance, IDbTransaction transaction, CancellationToken ct = default)
    {
        var maintenant = DateTime.UtcNow.ToString("O");
        var id = await Cn(transaction).QuerySingleAsync<long>($"""
            INSERT INTO {T("INSTANCE_PROCESSUS")}
                (CLE_DEFINITION, VERSION_DEFINITION, AGGREGATE_ID, STATUT,
                 ID_NOEUD_COURANT, ID_INSTANCE_PARENT, DATE_DEBUT, DATE_FIN,
                 DATE_CREATION, DATE_MAJ)
            VALUES
                (@CleDefinition, @VersionDefinition, @AggregateId, @Statut,
                 @IdNoeudCourant, @IdInstanceParent, @DateDebut, @DateFin,
                 @DateCreation, @DateMaj);
            SELECT last_insert_rowid()
            """,
            new
            {
                instance.CleDefinition,
                instance.VersionDefinition,
                instance.AggregateId,
                Statut = instance.Statut.ToString(),
                instance.IdNoeudCourant,
                instance.IdInstanceParent,
                DateDebut = instance.DateDebut.ToString("O"),
                DateFin = instance.DateFin?.ToString("O"),
                DateCreation = maintenant,
                DateMaj = maintenant
            }, transaction);

        return id;
    }

    public async Task<InstanceProcessus?> ObtenirParIdAsync(
        long id, IDbTransaction transaction, CancellationToken ct = default)
    {
        var row = await Cn(transaction).QuerySingleOrDefaultAsync($"""
            SELECT * FROM {T("INSTANCE_PROCESSUS")} WHERE ID = @Id
            """, new { Id = id }, transaction);
        return row is null ? null : MapperInstance(row);
    }

    public async Task<InstanceProcessus?> ObtenirActiveParAggregateAsync(
        string cleDefinition, long aggregateId,
        IDbTransaction transaction, CancellationToken ct = default)
    {
        var row = await Cn(transaction).QuerySingleOrDefaultAsync($"""
            SELECT * FROM {T("INSTANCE_PROCESSUS")}
            WHERE CLE_DEFINITION = @Cle
              AND AGGREGATE_ID = @AggId
              AND STATUT != 'Terminee'
            LIMIT 1
            """, new { Cle = cleDefinition, AggId = aggregateId }, transaction);
        return row is null ? null : MapperInstance(row);
    }

    public async Task<IReadOnlyList<InstanceProcessus>> ObtenirEnfantsAsync(
        long idParent, IDbTransaction transaction, CancellationToken ct = default)
    {
        var rows = await Cn(transaction).QueryAsync($"""
            SELECT * FROM {T("INSTANCE_PROCESSUS")} WHERE ID_INSTANCE_PARENT = @IdParent
            """, new { IdParent = idParent }, transaction);
        return rows.Select(r => (InstanceProcessus)MapperInstance(r)).ToList();
    }

    public async Task<IReadOnlyList<InstanceProcessus>> RechercherParVariableAsync(
        string nomVariable, string valeurSerialisee,
        IDbTransaction transaction, CancellationToken ct = default)
    {
        var rows = await Cn(transaction).QueryAsync($"""
            SELECT i.* FROM {T("INSTANCE_PROCESSUS")} i
            JOIN {T("VARIABLE_PROCESSUS")} v ON v.ID_INSTANCE = i.ID
            WHERE v.NOM = @Nom AND v.VALEUR = @Valeur
            """, new { Nom = nomVariable, Valeur = valeurSerialisee }, transaction);
        return rows.Select(r => (InstanceProcessus)MapperInstance(r)).ToList();
    }

    public async Task<IReadOnlyList<InstanceProcessus>> ObtenirSuspenduesAsync(
        IDbTransaction transaction, CancellationToken ct = default)
    {
        var rows = await Cn(transaction).QueryAsync($"""
            SELECT * FROM {T("INSTANCE_PROCESSUS")}
            WHERE STATUT IN ('Active', 'Suspendue')
            """, transaction: transaction);
        return rows.Select(r => (InstanceProcessus)MapperInstance(r)).ToList();
    }

    public async Task MettreAJourStatutAsync(
        long id, StatutInstance statut, string? idNoeudCourant, DateTime? dateFin,
        IDbTransaction transaction, CancellationToken ct = default)
    {
        await Cn(transaction).ExecuteAsync($"""
            UPDATE {T("INSTANCE_PROCESSUS")}
            SET STATUT = @Statut,
                ID_NOEUD_COURANT = @NoeudCourant,
                DATE_FIN = @DateFin,
                DATE_MAJ = @DateMaj
            WHERE ID = @Id
            """,
            new
            {
                Id = id,
                Statut = statut.ToString(),
                NoeudCourant = idNoeudCourant,
                DateFin = dateFin?.ToString("O"),
                DateMaj = DateTime.UtcNow.ToString("O")
            }, transaction);
    }

    public async Task MettreAJourVersionAsync(
        long id, int nouvelleVersion, string? idNoeudCourant,
        IDbTransaction transaction, CancellationToken ct = default)
    {
        await Cn(transaction).ExecuteAsync($"""
            UPDATE {T("INSTANCE_PROCESSUS")}
            SET VERSION_DEFINITION = @Version,
                ID_NOEUD_COURANT = @NoeudCourant,
                DATE_MAJ = @DateMaj
            WHERE ID = @Id
            """,
            new
            {
                Id = id,
                Version = nouvelleVersion,
                NoeudCourant = idNoeudCourant,
                DateMaj = DateTime.UtcNow.ToString("O")
            }, transaction);
    }

    public async Task<bool> ExisteProcessusActifAsync(
        string cleDefinition, long aggregateId,
        IDbTransaction transaction, CancellationToken ct = default)
    {
        var count = await Cn(transaction).QuerySingleAsync<int>($"""
            SELECT COUNT(*) FROM {T("INSTANCE_PROCESSUS")}
            WHERE CLE_DEFINITION = @Cle
              AND AGGREGATE_ID = @AggId
              AND STATUT != 'Terminee'
            """, new { Cle = cleDefinition, AggId = aggregateId }, transaction);
        return count > 0;
    }

    private static InstanceProcessus MapperInstance(dynamic row) => new()
    {
        Id = (long)row.ID,
        CleDefinition = (string)row.CLE_DEFINITION,
        VersionDefinition = (int)row.VERSION_DEFINITION,
        AggregateId = (long)row.AGGREGATE_ID,
        Statut = Enum.Parse<StatutInstance>((string)row.STATUT),
        IdNoeudCourant = row.ID_NOEUD_COURANT,
        IdInstanceParent = row.ID_INSTANCE_PARENT,
        DateDebut = DateTime.Parse((string)row.DATE_DEBUT),
        DateFin = row.DATE_FIN is not null ? DateTime.Parse((string)row.DATE_FIN) : null,
        DateCreation = DateTime.Parse((string)row.DATE_CREATION),
        DateMaj = DateTime.Parse((string)row.DATE_MAJ)
    };
}
