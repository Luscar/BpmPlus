using System.Data;
using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Oracle.Repositories;

public class RepositoryEvenementOracle : OracleRepositoryBase, IRepositoryEvenement
{
    public RepositoryEvenementOracle(string prefixe) : base(prefixe) { }

    public Task CreerTablesAsync(IDbConnection connection) => Task.CompletedTask;

    public async Task AjouterAsync(
        EvenementInstance evenement, IDbTransaction transaction, CancellationToken ct = default)
    {
        await Cn(transaction).ExecuteAsync(OraParam($"""
            INSERT INTO {T("EVENEMENT_INSTANCE")}
                (ID, ID_INSTANCE, TYPE_EVENEMENT, ID_NOEUD, NOM_NOEUD, HORODATAGE, DUREE_MS, RESULTAT, DETAIL)
            VALUES
                ({T("SEQ_EVENEMENT")}.NEXTVAL, :IdInstance, :TypeEvenement, :IdNoeud, :NomNoeud,
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
            }, transaction);
    }

    public async Task<IReadOnlyList<EvenementInstance>> ObtenirParInstanceAsync(
        long idInstance, IDbTransaction transaction, CancellationToken ct = default)
    {
        var rows = await Cn(transaction).QueryAsync(OraParam($"""
            SELECT * FROM {T("EVENEMENT_INSTANCE")}
            WHERE ID_INSTANCE = :IdInstance
            ORDER BY ID
            """), new { IdInstance = idInstance }, transaction);

        return rows.Select(MapperEvenement).ToList();
    }

    public async Task<EvenementInstance?> ObtenirDernierSuspensionAsync(
        long idInstance, IDbTransaction transaction, CancellationToken ct = default)
    {
        var row = await Cn(transaction).QuerySingleOrDefaultAsync(OraParam($"""
            SELECT * FROM {T("EVENEMENT_INSTANCE")}
            WHERE ID_INSTANCE = :IdInstance
              AND TYPE_EVENEMENT = 'NoeudSuspendu'
            ORDER BY ID DESC
            FETCH FIRST 1 ROW ONLY
            """), new { IdInstance = idInstance }, transaction);

        return row is null ? null : MapperEvenement(row);
    }

    private static EvenementInstance MapperEvenement(dynamic row) => new()
    {
        Id = Convert.ToInt64(row.ID),
        IdInstance = Convert.ToInt64(row.ID_INSTANCE),
        TypeEvenement = Enum.Parse<TypeEvenement>((string)row.TYPE_EVENEMENT),
        IdNoeud = row.ID_NOEUD,
        NomNoeud = row.NOM_NOEUD,
        Horodatage = Convert.ToDateTime(row.HORODATAGE),
        DureeMs = row.DUREE_MS is not null ? Convert.ToInt64(row.DUREE_MS) : null,
        Resultat = row.RESULTAT is not null
            ? Enum.Parse<ResultatEvenement>((string)row.RESULTAT) : null,
        Detail = row.DETAIL
    };
}
