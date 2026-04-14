using System.Data;
using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Oracle.Repositories;

public class RepositoryInstanceOracle : OracleRepositoryBase, IRepositoryInstance
{
    public RepositoryInstanceOracle(string prefixe) : base(prefixe) { }

    public Task CreerTablesAsync(IDbConnection connection) => Task.CompletedTask;

    public async Task<long> CreerAsync(
        InstanceProcessus instance, IDbTransaction transaction, CancellationToken ct = default)
    {
        var maintenant = DateTime.UtcNow;
        var id = await Cn(transaction).QuerySingleAsync<long>(OraParam($"""
            INSERT INTO {T("INSTANCE_PROCESSUS")}
                (ID, CLE_DEFINITION, VERSION_DEFINITION, AGGREGATE_ID, STATUT,
                 ID_NOEUD_COURANT, ID_INSTANCE_PARENT, DATE_DEBUT, DATE_FIN,
                 DATE_CREATION, DATE_MAJ)
            VALUES
                ({T("SEQ_INSTANCE")}.NEXTVAL, :CleDefinition, :VersionDefinition, :AggregateId, :Statut,
                 :IdNoeudCourant, :IdInstanceParent, :DateDebut, :DateFin,
                 :DateCreation, :DateMaj)
            RETURNING ID INTO :NewId
            """),
            new
            {
                instance.CleDefinition,
                instance.VersionDefinition,
                instance.AggregateId,
                Statut = instance.Statut.ToString(),
                instance.IdNoeudCourant,
                instance.IdInstanceParent,
                DateDebut = instance.DateDebut,
                DateFin = instance.DateFin,
                DateCreation = maintenant,
                DateMaj = maintenant
            }, transaction);

        return id;
    }

    public async Task<InstanceProcessus?> ObtenirParIdAsync(
        long id, IDbTransaction transaction, CancellationToken ct = default)
    {
        var row = await Cn(transaction).QuerySingleOrDefaultAsync(OraParam($"""
            SELECT * FROM {T("INSTANCE_PROCESSUS")} WHERE ID = :Id
            """), new { Id = id }, transaction);
        return row is null ? null : MapperInstance(row);
    }

    public async Task<InstanceProcessus?> ObtenirActiveParAggregateAsync(
        string cleDefinition, long aggregateId,
        IDbTransaction transaction, CancellationToken ct = default)
    {
        var row = await Cn(transaction).QuerySingleOrDefaultAsync(OraParam($"""
            SELECT * FROM {T("INSTANCE_PROCESSUS")}
            WHERE CLE_DEFINITION = :Cle
              AND AGGREGATE_ID = :AggId
              AND STATUT != 'Terminee'
            FETCH FIRST 1 ROW ONLY
            """), new { Cle = cleDefinition, AggId = aggregateId }, transaction);
        return row is null ? null : MapperInstance(row);
    }

    public async Task<IReadOnlyList<InstanceProcessus>> ObtenirEnfantsAsync(
        long idParent, IDbTransaction transaction, CancellationToken ct = default)
    {
        var rows = await Cn(transaction).QueryAsync(OraParam($"""
            SELECT * FROM {T("INSTANCE_PROCESSUS")} WHERE ID_INSTANCE_PARENT = :IdParent
            """), new { IdParent = idParent }, transaction);
        return rows.Select(r => (InstanceProcessus)MapperInstance(r)).ToList();
    }

    public async Task<IReadOnlyList<InstanceProcessus>> RechercherParVariableAsync(
        string nomVariable, string valeurSerialisee,
        IDbTransaction transaction, CancellationToken ct = default)
    {
        var rows = await Cn(transaction).QueryAsync(OraParam($"""
            SELECT i.* FROM {T("INSTANCE_PROCESSUS")} i
            JOIN {T("VARIABLE_PROCESSUS")} v ON v.ID_INSTANCE = i.ID
            WHERE v.NOM = :Nom AND v.VALEUR = :Valeur
            """), new { Nom = nomVariable, Valeur = valeurSerialisee }, transaction);
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
        await Cn(transaction).ExecuteAsync(OraParam($"""
            UPDATE {T("INSTANCE_PROCESSUS")}
            SET STATUT = :Statut,
                ID_NOEUD_COURANT = :NoeudCourant,
                DATE_FIN = :DateFin,
                DATE_MAJ = :DateMaj
            WHERE ID = :Id
            """),
            new
            {
                Id = id,
                Statut = statut.ToString(),
                NoeudCourant = idNoeudCourant,
                DateFin = dateFin,
                DateMaj = DateTime.UtcNow
            }, transaction);
    }

    public async Task MettreAJourVersionAsync(
        long id, int nouvelleVersion, string? idNoeudCourant,
        IDbTransaction transaction, CancellationToken ct = default)
    {
        await Cn(transaction).ExecuteAsync(OraParam($"""
            UPDATE {T("INSTANCE_PROCESSUS")}
            SET VERSION_DEFINITION = :Version,
                ID_NOEUD_COURANT = :NoeudCourant,
                DATE_MAJ = :DateMaj
            WHERE ID = :Id
            """),
            new { Id = id, Version = nouvelleVersion, NoeudCourant = idNoeudCourant, DateMaj = DateTime.UtcNow },
            transaction);
    }

    public async Task<bool> ExisteProcessusActifAsync(
        string cleDefinition, long aggregateId,
        IDbTransaction transaction, CancellationToken ct = default)
    {
        var count = await Cn(transaction).QuerySingleAsync<int>(OraParam($"""
            SELECT COUNT(*) FROM {T("INSTANCE_PROCESSUS")}
            WHERE CLE_DEFINITION = :Cle
              AND AGGREGATE_ID = :AggId
              AND STATUT != 'Terminee'
            """), new { Cle = cleDefinition, AggId = aggregateId }, transaction);
        return count > 0;
    }

    private static InstanceProcessus MapperInstance(dynamic row) => new()
    {
        Id = Convert.ToInt64(row.ID),
        CleDefinition = (string)row.CLE_DEFINITION,
        VersionDefinition = Convert.ToInt32(row.VERSION_DEFINITION),
        AggregateId = Convert.ToInt64(row.AGGREGATE_ID),
        Statut = Enum.Parse<StatutInstance>((string)row.STATUT),
        IdNoeudCourant = row.ID_NOEUD_COURANT,
        IdInstanceParent = row.ID_INSTANCE_PARENT is not null
            ? Convert.ToInt64(row.ID_INSTANCE_PARENT) : null,
        DateDebut = Convert.ToDateTime(row.DATE_DEBUT),
        DateFin = row.DATE_FIN is not null ? Convert.ToDateTime(row.DATE_FIN) : null,
        DateCreation = Convert.ToDateTime(row.DATE_CREATION),
        DateMaj = Convert.ToDateTime(row.DATE_MAJ)
    };
}
