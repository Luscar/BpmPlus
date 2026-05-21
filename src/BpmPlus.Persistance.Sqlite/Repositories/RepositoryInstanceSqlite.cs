using System.Data;
using System.Text;
using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Sqlite.Repositories;

public class RepositoryInstanceSqlite : SqliteRepositoryBase, IRepositoryInstance
{
    public RepositoryInstanceSqlite(IDbConnection connection, string prefixe, IDbTransaction? tx = null) : base(connection, prefixe, tx) { }

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
                DATE_MAJ            TEXT    NOT NULL,
                LOGON_ASSIGNE       TEXT    NULL,
                LOGON_TACHE_PREC    TEXT    NULL,
                ID_TACHE_EXTERNE    INTEGER NULL
            );
            CREATE INDEX IF NOT EXISTS IDX_{Prefixe}_INST_AGGID
                ON {T("INSTANCE_PROCESSUS")}(AGGREGATE_ID);
            """);
    }

    public async Task<long> CreerAsync(InstanceProcessus instance, CancellationToken ct = default)
    {
        var maintenant = DateTime.UtcNow.ToString("O");
        var id = await Cn.QuerySingleAsync<long>($"""
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
            },
            Tx);

        return id;
    }

    public async Task<InstanceProcessus?> ObtenirParIdAsync(long id, CancellationToken ct = default)
    {
        var row = await Cn.QuerySingleOrDefaultAsync($"""
            SELECT * FROM {T("INSTANCE_PROCESSUS")} WHERE ID = @Id
            """, new { Id = id }, Tx);
        return row is null ? null : MapperInstance(row);
    }

    public async Task<InstanceProcessus?> ObtenirActiveParAggregateAsync(
        string cleDefinition, long aggregateId, CancellationToken ct = default)
    {
        var row = await Cn.QuerySingleOrDefaultAsync($"""
            SELECT * FROM {T("INSTANCE_PROCESSUS")}
            WHERE CLE_DEFINITION = @Cle
              AND AGGREGATE_ID = @AggId
              AND STATUT != 'Terminee'
            LIMIT 1
            """, new { Cle = cleDefinition, AggId = aggregateId }, Tx);
        return row is null ? null : MapperInstance(row);
    }

    public async Task<IReadOnlyList<InstanceProcessus>> ObtenirEnfantsAsync(
        long idParent, CancellationToken ct = default)
    {
        var rows = await Cn.QueryAsync($"""
            SELECT * FROM {T("INSTANCE_PROCESSUS")} WHERE ID_INSTANCE_PARENT = @IdParent
            """, new { IdParent = idParent }, Tx);
        return rows.Select(r => (InstanceProcessus)MapperInstance(r)).ToList();
    }

    public async Task<IReadOnlyList<InstanceProcessus>> RechercherParVariableAsync(
        string nomVariable, string valeurSerialisee, CancellationToken ct = default)
    {
        var rows = await Cn.QueryAsync($"""
            SELECT i.* FROM {T("INSTANCE_PROCESSUS")} i
            JOIN {T("VARIABLE_PROCESSUS")} v ON v.ID_INSTANCE = i.ID
            WHERE v.NOM = @Nom AND v.VALEUR = @Valeur
            """, new { Nom = nomVariable, Valeur = valeurSerialisee }, Tx);
        return rows.Select(r => (InstanceProcessus)MapperInstance(r)).ToList();
    }

    public async Task<IReadOnlyList<InstanceProcessus>> RechercherParVariableAsync(
        string nomVariable, string valeurSerialisee, StatutInstance statut, CancellationToken ct = default)
    {
        var rows = await Cn.QueryAsync($"""
            SELECT i.* FROM {T("INSTANCE_PROCESSUS")} i
            JOIN {T("VARIABLE_PROCESSUS")} v ON v.ID_INSTANCE = i.ID
            WHERE v.NOM = @Nom AND v.VALEUR = @Valeur AND i.STATUT = @Statut
            """, new { Nom = nomVariable, Valeur = valeurSerialisee, Statut = statut.ToString() }, Tx);
        return rows.Select(r => (InstanceProcessus)MapperInstance(r)).ToList();
    }

    public async Task<IReadOnlyList<InstanceProcessus>> RechercherParVariablesAsync(
        IReadOnlyList<FiltreVariableSerialisee> filtres, StatutInstance? statut = null, CancellationToken ct = default)
    {
        var sql = new StringBuilder($"SELECT * FROM {T("INSTANCE_PROCESSUS")} i WHERE");
        var dp = new DynamicParameters();

        for (int i = 0; i < filtres.Count; i++)
        {
            var f = filtres[i];
            var nomParam = $"Nom{i}";
            var valParam = $"Valeur{i}";
            var valeur = f.Operateur == Operateur.Contient ? $"%{f.ValeurSerialisee}%" : f.ValeurSerialisee;

            if (i > 0) sql.Append(" AND");
            sql.Append($" EXISTS (SELECT 1 FROM {T("VARIABLE_PROCESSUS")} WHERE ID_INSTANCE = i.ID AND NOM = @{nomParam} AND VALEUR {OperateurVersSql(f.Operateur)} @{valParam})");
            dp.Add(nomParam, f.NomVariable);
            dp.Add(valParam, valeur);
        }

        if (statut.HasValue)
        {
            if (filtres.Count == 0) sql.Append(" i.STATUT = @Statut");
            else sql.Append(" AND i.STATUT = @Statut");
            dp.Add("Statut", statut.Value.ToString());
        }
        else if (filtres.Count == 0)
        {
            sql.Append(" 1=1");
        }

        var rows = await Cn.QueryAsync(sql.ToString(), dp, Tx);
        return rows.Select(r => (InstanceProcessus)MapperInstance(r)).ToList();
    }

    private static string OperateurVersSql(Operateur op) => op switch
    {
        Operateur.Different => "<>",
        Operateur.Superieur => ">",
        Operateur.Inferieur => "<",
        Operateur.SuperieurOuEgal => ">=",
        Operateur.InferieurOuEgal => "<=",
        Operateur.Contient => "LIKE",
        _ => "="
    };

    public async Task<IReadOnlyList<InstanceProcessus>> ObtenirParStatutAsync(
        StatutInstance statut, CancellationToken ct = default)
    {
        var rows = await Cn.QueryAsync($"""
            SELECT * FROM {T("INSTANCE_PROCESSUS")} WHERE STATUT = @Statut
            """, new { Statut = statut.ToString() }, Tx);
        return rows.Select(r => (InstanceProcessus)MapperInstance(r)).ToList();
    }

    public async Task<IReadOnlyList<InstanceProcessus>> ObtenirSuspenduesAsync(CancellationToken ct = default)
    {
        var rows = await Cn.QueryAsync($"""
            SELECT * FROM {T("INSTANCE_PROCESSUS")}
            WHERE STATUT IN ('Active', 'Suspendue')
            """, null, Tx);
        return rows.Select(r => (InstanceProcessus)MapperInstance(r)).ToList();
    }

    public async Task MettreAJourStatutAsync(
        long id, StatutInstance statut, string? idNoeudCourant, DateTime? dateFin,
        CancellationToken ct = default)
    {
        await Cn.ExecuteAsync($"""
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
            },
            Tx);
    }

    public async Task MettreAJourVersionAsync(
        long id, int nouvelleVersion, string? idNoeudCourant, CancellationToken ct = default)
    {
        await Cn.ExecuteAsync($"""
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
            },
            Tx);
    }

    public async Task MettreAJourLogonsAsync(
        long id, string? logonAssigne, string? logonTachePrecedente, long? idTacheExterne, CancellationToken ct = default)
    {
        await Cn.ExecuteAsync($"""
            UPDATE {T("INSTANCE_PROCESSUS")}
            SET LOGON_ASSIGNE     = @LogonAssigne,
                LOGON_TACHE_PREC  = @LogonTachePrecedente,
                ID_TACHE_EXTERNE  = @IdTacheExterne,
                DATE_MAJ          = @DateMaj
            WHERE ID = @Id
            """,
            new
            {
                Id = id,
                LogonAssigne = logonAssigne,
                LogonTachePrecedente = logonTachePrecedente,
                IdTacheExterne = idTacheExterne,
                DateMaj = DateTime.UtcNow.ToString("O")
            },
            Tx);
    }

    public async Task<bool> ExisteProcessusActifAsync(
        string cleDefinition, long aggregateId, CancellationToken ct = default)
    {
        var count = await Cn.QuerySingleAsync<int>($"""
            SELECT COUNT(*) FROM {T("INSTANCE_PROCESSUS")}
            WHERE CLE_DEFINITION = @Cle
              AND AGGREGATE_ID = @AggId
              AND STATUT != 'Terminee'
            """, new { Cle = cleDefinition, AggId = aggregateId }, Tx);
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
        DateMaj = DateTime.Parse((string)row.DATE_MAJ),
        LogonAssigne = row.LOGON_ASSIGNE,
        LogonTachePrecedente = row.LOGON_TACHE_PREC,
        IdTacheExterne = row.ID_TACHE_EXTERNE
    };
}
