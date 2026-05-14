using System.Data;
using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Oracle.Repositories;

public class RepositoryEvenementOracle : OracleRepositoryBase, IRepositoryEvenement
{
    public RepositoryEvenementOracle(IDbConnection connection, string prefixe, IDbTransaction? tx = null) : base(connection, prefixe, tx) { }

    public Task CreerTablesAsync(IDbConnection connection) => Task.CompletedTask;

    public async Task AjouterAsync(EvenementInstance evenement, CancellationToken ct = default)
    {
        await Cn.ExecuteAsync(OraParam($"""
            INSERT INTO {T("EVEN_INSTC_PROCS")}
                (NO_SEQ_EVEN_INSTC_PROCS, NO_SEQ_INSTC_PROCS, TYP_EVEN, ID_NOEUD, NOM_NOEUD, DH_CREA, DUR_MS, RESUL, DETL)
            VALUES
                ({T("SEQ_EVEN_INSTC")}.NEXTVAL, :IdInstance, :TypeEvenement, :IdNoeud, :NomNoeud,
                 :Horodatage, :DureeMs, :Resultat, :Detail)
            """),
            new
            {
                evenement.IdInstance,
                TypeEvenement = evenement.TypeEvenement.ToString(),
                evenement.IdNoeud,
                evenement.NomNoeud,
                Horodatage = evenement.Horodatage,
                evenement.DureeMs,
                Resultat = evenement.Resultat?.ToString(),
                evenement.Detail
            });
    }

    public async Task<IReadOnlyList<EvenementInstance>> ObtenirParInstanceAsync(
        long idInstance, CancellationToken ct = default)
    {
        var rows = await Cn.QueryAsync(OraParam($"""
            SELECT * FROM {T("EVEN_INSTC_PROCS")}
            WHERE NO_SEQ_INSTC_PROCS = :IdInstance
            ORDER BY NO_SEQ_EVEN_INSTC_PROCS
            """), new { IdInstance = idInstance });

        return rows.Select(MapperEvenement).ToList();
    }

    public async Task<EvenementInstance?> ObtenirDernierSuspensionAsync(
        long idInstance, CancellationToken ct = default)
    {
        var row = await Cn.QuerySingleOrDefaultAsync(OraParam($"""
            SELECT * FROM {T("EVEN_INSTC_PROCS")}
            WHERE NO_SEQ_INSTC_PROCS = :IdInstance
              AND TYP_EVEN = 'NoeudSuspendu'
            ORDER BY NO_SEQ_EVEN_INSTC_PROCS DESC
            FETCH FIRST 1 ROW ONLY
            """), new { IdInstance = idInstance });

        return row is null ? null : MapperEvenement(row);
    }

    public async Task<EvenementInstance?> ObtenirDernierParTypeAsync(
        long idInstance, TypeEvenement type, CancellationToken ct = default)
    {
        var row = await Cn.QuerySingleOrDefaultAsync(OraParam($"""
            SELECT * FROM {T("EVEN_INSTC_PROCS")}
            WHERE NO_SEQ_INSTC_PROCS = :IdInstance
              AND TYP_EVEN = :TypeEvenement
            ORDER BY NO_SEQ_EVEN_INSTC_PROCS DESC
            FETCH FIRST 1 ROW ONLY
            """), new { IdInstance = idInstance, TypeEvenement = type.ToString() });

        return row is null ? null : MapperEvenement(row);
    }

    private static EvenementInstance MapperEvenement(dynamic row) => new()
    {
        Id = Convert.ToInt64(row.NO_SEQ_EVEN_INSTC_PROCS),
        IdInstance = Convert.ToInt64(row.NO_SEQ_INSTC_PROCS),
        TypeEvenement = Enum.Parse<TypeEvenement>((string)row.TYP_EVEN),
        IdNoeud = row.ID_NOEUD,
        NomNoeud = row.NOM_NOEUD,
        Horodatage = Convert.ToDateTime(row.DH_CREA),
        DureeMs = row.DUR_MS is not null ? Convert.ToInt64(row.DUR_MS) : null,
        Resultat = row.RESUL is not null
            ? Enum.Parse<ResultatEvenement>((string)row.RESUL) : null,
        Detail = row.DETL
    };
}
