using System.Data;
using System.Text;
using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Oracle.Repositories;

public class RepositoryInstanceOracle : OracleRepositoryBase, IRepositoryInstance
{
    public RepositoryInstanceOracle(Lazy<IDbConnection> connection, string prefixe, IDbTransaction? tx = null) : base(connection, prefixe, tx) { }

    public Task CreerTablesAsync(IDbConnection connection) => Task.CompletedTask;

    public async Task<long> CreerAsync(InstanceProcessus instance, CancellationToken ct = default)
    {
        var maintenant = DateTime.UtcNow;
        var dp = new DynamicParameters();
        dp.Add("CleDefinition", instance.CleDefinition);
        dp.Add("VersionDefinition", instance.VersionDefinition);
        dp.Add("AggregateId", instance.AggregateId);
        dp.Add("Statut", instance.Statut.ToString());
        dp.Add("IdNoeudCourant", instance.IdNoeudCourant);
        dp.Add("IdInstanceParent", instance.IdInstanceParent);
        dp.Add("DateDebut", instance.DateDebut);
        dp.Add("DateFin", instance.DateFin);
        dp.Add("DateCreation", maintenant);
        dp.Add("DateMaj", maintenant);
        dp.Add("NewId", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await Cn.ExecuteAsync(OraParam($"""
            INSERT INTO {T("INSTC_PROCS")}
                (NO_SEQ_INSTC_PROCS, CLE_DEFIN, VERSI_DEFIN, AGGRE_ID, STAT,
                 ID_NOEUD_COUR, ID_INSTC_PARN, DH_DEB, DH_FIN,
                 DH_CREA, DH_MODIF)
            VALUES
                ({T("SEQ_INSTC")}.NEXTVAL, :CleDefinition, :VersionDefinition, :AggregateId, :Statut,
                 :IdNoeudCourant, :IdInstanceParent, :DateDebut, :DateFin,
                 :DateCreation, :DateMaj)
            RETURNING NO_SEQ_INSTC_PROCS INTO :NewId
            """), dp, Tx);

        return dp.Get<long>("NewId");
    }

    public async Task<InstanceProcessus?> ObtenirParIdAsync(long id, CancellationToken ct = default)
    {
        var row = await Cn.QuerySingleOrDefaultAsync(OraParam($"""
            SELECT * FROM {T("INSTC_PROCS")} WHERE NO_SEQ_INSTC_PROCS = :Id
            """), new { Id = id });
        return row is null ? null : MapperInstance(row);
    }

    public async Task<InstanceProcessus?> ObtenirActiveParAggregateAsync(
        string cleDefinition, long aggregateId, CancellationToken ct = default)
    {
        var row = await Cn.QuerySingleOrDefaultAsync(OraParam($"""
            SELECT * FROM {T("INSTC_PROCS")}
            WHERE CLE_DEFIN = :Cle
              AND AGGRE_ID = :AggId
              AND STAT != 'Terminee'
            FETCH FIRST 1 ROW ONLY
            """), new { Cle = cleDefinition, AggId = aggregateId });
        return row is null ? null : MapperInstance(row);
    }

    public async Task<IReadOnlyList<InstanceProcessus>> ObtenirEnfantsAsync(
        long idParent, CancellationToken ct = default)
    {
        var rows = await Cn.QueryAsync(OraParam($"""
            SELECT * FROM {T("INSTC_PROCS")} WHERE ID_INSTC_PARN = :IdParent
            """), new { IdParent = idParent });
        return rows.Select(r => (InstanceProcessus)MapperInstance(r)).ToList();
    }

    public async Task<IReadOnlyList<InstanceProcessus>> RechercherParVariableAsync(
        string nomVariable, string valeurSerialisee, CancellationToken ct = default)
    {
        var rows = await Cn.QueryAsync(OraParam($"""
            SELECT i.* FROM {T("INSTC_PROCS")} i
            JOIN {T("VAR_PROCS")} v ON v.NO_SEQ_INSTC_PROCS = i.NO_SEQ_INSTC_PROCS
            WHERE v.NOM_VAR = :Nom AND v.VAL_VAR = :Valeur
            """), new { Nom = nomVariable, Valeur = valeurSerialisee });
        return rows.Select(r => (InstanceProcessus)MapperInstance(r)).ToList();
    }

    public async Task<IReadOnlyList<InstanceProcessus>> RechercherParVariableAsync(
        string nomVariable, string valeurSerialisee, StatutInstance statut, CancellationToken ct = default)
    {
        var rows = await Cn.QueryAsync(OraParam($"""
            SELECT i.* FROM {T("INSTC_PROCS")} i
            JOIN {T("VAR_PROCS")} v ON v.NO_SEQ_INSTC_PROCS = i.NO_SEQ_INSTC_PROCS
            WHERE v.NOM_VAR = :Nom AND v.VAL_VAR = :Valeur AND i.STAT = :Statut
            """), new { Nom = nomVariable, Valeur = valeurSerialisee, Statut = statut.ToString() });
        return rows.Select(r => (InstanceProcessus)MapperInstance(r)).ToList();
    }

    public async Task<IReadOnlyList<InstanceProcessus>> RechercherParVariablesAsync(
        IReadOnlyList<FiltreVariableSerialisee> filtres, StatutInstance? statut = null, CancellationToken ct = default)
    {
        var sql = new StringBuilder($"SELECT * FROM {T("INSTC_PROCS")} i WHERE");
        var dp = new DynamicParameters();

        for (int i = 0; i < filtres.Count; i++)
        {
            var f = filtres[i];
            var nomParam = $"Nom{i}";
            var valParam = $"Valeur{i}";
            var valeur = f.Operateur == Operateur.Contient ? $"%{f.ValeurSerialisee}%" : f.ValeurSerialisee;

            if (i > 0) sql.Append(" AND");
            sql.Append($" EXISTS (SELECT 1 FROM {T("VAR_PROCS")} WHERE NO_SEQ_INSTC_PROCS = i.NO_SEQ_INSTC_PROCS AND NOM_VAR = @{nomParam} AND VAL_VAR {OperateurVersSql(f.Operateur)} @{valParam})");
            dp.Add(nomParam, f.NomVariable);
            dp.Add(valParam, valeur);
        }

        if (statut.HasValue)
        {
            if (filtres.Count == 0) sql.Append(" i.STAT = @Statut");
            else sql.Append(" AND i.STAT = @Statut");
            dp.Add("Statut", statut.Value.ToString());
        }
        else if (filtres.Count == 0)
        {
            sql.Append(" 1=1");
        }

        var rows = await Cn.QueryAsync(OraParam(sql.ToString()), dp);
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
        var rows = await Cn.QueryAsync(OraParam($"""
            SELECT * FROM {T("INSTC_PROCS")} WHERE STAT = :Statut
            """), new { Statut = statut.ToString() });
        return rows.Select(r => (InstanceProcessus)MapperInstance(r)).ToList();
    }

    public async Task<IReadOnlyList<InstanceProcessus>> ObtenirSuspenduesAsync(CancellationToken ct = default)
    {
        var rows = await Cn.QueryAsync($"""
            SELECT * FROM {T("INSTC_PROCS")}
            WHERE STAT IN ('Active', 'Suspendue')
            """);
        return rows.Select(r => (InstanceProcessus)MapperInstance(r)).ToList();
    }

    public async Task MettreAJourStatutAsync(
        long id, StatutInstance statut, string? idNoeudCourant, DateTime? dateFin,
        CancellationToken ct = default)
    {
        await Cn.ExecuteAsync(OraParam($"""
            UPDATE {T("INSTC_PROCS")}
            SET STAT = :Statut,
                ID_NOEUD_COUR = :NoeudCourant,
                DH_FIN = :DateFin,
                DH_MODIF = :DateMaj
            WHERE NO_SEQ_INSTC_PROCS = :Id
            """),
            new
            {
                Id = id,
                Statut = statut.ToString(),
                NoeudCourant = idNoeudCourant,
                DateFin = dateFin,
                DateMaj = DateTime.UtcNow
            }, Tx);
    }

    public async Task MettreAJourVersionAsync(
        long id, int nouvelleVersion, string? idNoeudCourant, CancellationToken ct = default)
    {
        await Cn.ExecuteAsync(OraParam($"""
            UPDATE {T("INSTC_PROCS")}
            SET VERSI_DEFIN = :Version,
                ID_NOEUD_COUR = :NoeudCourant,
                DH_MODIF = :DateMaj
            WHERE NO_SEQ_INSTC_PROCS = :Id
            """),
            new { Id = id, Version = nouvelleVersion, NoeudCourant = idNoeudCourant, DateMaj = DateTime.UtcNow },
            Tx);
    }

    public async Task MettreAJourLogonsAsync(
        long id, string? logonAssigne, string? logonTachePrecedente, string? idTacheExterne, CancellationToken ct = default)
    {
        await Cn.ExecuteAsync(OraParam($"""
            UPDATE {T("INSTC_PROCS")}
            SET LOGON_ASSIG     = :LogonAssigne,
                LOGON_TACH_PREC = :LogonTachePrecedente,
                ID_TACH_EXTE    = :IdTacheExterne,
                DH_MODIF        = :DateMaj
            WHERE NO_SEQ_INSTC_PROCS = :Id
            """),
            new { Id = id, LogonAssigne = logonAssigne, LogonTachePrecedente = logonTachePrecedente, IdTacheExterne = idTacheExterne, DateMaj = DateTime.UtcNow },
            Tx);
    }

    public async Task<bool> ExisteProcessusActifAsync(
        string cleDefinition, long aggregateId, CancellationToken ct = default)
    {
        var count = await Cn.QuerySingleAsync<int>(OraParam($"""
            SELECT COUNT(*) FROM {T("INSTC_PROCS")}
            WHERE CLE_DEFIN = :Cle
              AND AGGRE_ID = :AggId
              AND STAT != 'Terminee'
            """), new { Cle = cleDefinition, AggId = aggregateId });
        return count > 0;
    }

    private static InstanceProcessus MapperInstance(dynamic row) => new()
    {
        Id = Convert.ToInt64(row.NO_SEQ_INSTC_PROCS),
        CleDefinition = (string)row.CLE_DEFIN,
        VersionDefinition = Convert.ToInt32(row.VERSI_DEFIN),
        AggregateId = Convert.ToInt64(row.AGGRE_ID),
        Statut = Enum.Parse<StatutInstance>((string)row.STAT),
        IdNoeudCourant = row.ID_NOEUD_COUR,
        IdInstanceParent = row.ID_INSTC_PARN is not null
            ? Convert.ToInt64(row.ID_INSTC_PARN) : null,
        DateDebut = Convert.ToDateTime(row.DH_DEB),
        DateFin = row.DH_FIN is not null ? Convert.ToDateTime(row.DH_FIN) : null,
        DateCreation = Convert.ToDateTime(row.DH_CREA),
        DateMaj = Convert.ToDateTime(row.DH_MODIF),
        LogonAssigne = row.LOGON_ASSIG,
        LogonTachePrecedente = row.LOGON_TACH_PREC,
        IdTacheExterne = row.ID_TACH_EXTE
    };
}
