using System.Data;
using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Sqlite.Repositories;

public class RepositoryEvenementSqlite : SqliteRepositoryBase, IRepositoryEvenement
{
    public RepositoryEvenementSqlite(string prefixe) : base(prefixe) { }

    public async Task CreerTablesAsync(IDbConnection connection)
    {
        await connection.ExecuteAsync($"""
            CREATE TABLE IF NOT EXISTS {T("EVENEMENT_INSTANCE")} (
                ID              INTEGER PRIMARY KEY AUTOINCREMENT,
                ID_INSTANCE     INTEGER NOT NULL,
                TYPE_EVENEMENT  TEXT    NOT NULL,
                ID_NOEUD        TEXT    NULL,
                NOM_NOEUD       TEXT    NULL,
                HORODATAGE      TEXT    NOT NULL,
                DUREE_MS        INTEGER NULL,
                RESULTAT        TEXT    NULL,
                DETAIL          TEXT    NULL
            )
            """);
    }

    public async Task AjouterAsync(
        EvenementInstance evenement, IDbTransaction transaction, CancellationToken ct = default)
    {
        await Cn(transaction).ExecuteAsync($"""
            INSERT INTO {T("EVENEMENT_INSTANCE")}
                (ID_INSTANCE, TYPE_EVENEMENT, ID_NOEUD, NOM_NOEUD, HORODATAGE, DUREE_MS, RESULTAT, DETAIL)
            VALUES
                (@IdInstance, @TypeEvenement, @IdNoeud, @NomNoeud, @Horodatage, @DureeMs, @Resultat, @Detail)
            """,
            new
            {
                evenement.IdInstance,
                TypeEvenement = evenement.TypeEvenement.ToString(),
                evenement.IdNoeud,
                evenement.NomNoeud,
                Horodatage = evenement.Horodatage.ToString("O"),
                evenement.DureeMs,
                Resultat = evenement.Resultat?.ToString(),
                evenement.Detail
            }, transaction);
    }

    public async Task<IReadOnlyList<EvenementInstance>> ObtenirParInstanceAsync(
        long idInstance, IDbTransaction transaction, CancellationToken ct = default)
    {
        var rows = await Cn(transaction).QueryAsync($"""
            SELECT * FROM {T("EVENEMENT_INSTANCE")}
            WHERE ID_INSTANCE = @IdInstance
            ORDER BY ID
            """, new { IdInstance = idInstance }, transaction);

        return rows.Select(MapperEvenement).ToList();
    }

    public async Task<EvenementInstance?> ObtenirDernierSuspensionAsync(
        long idInstance, IDbTransaction transaction, CancellationToken ct = default)
    {
        var row = await Cn(transaction).QuerySingleOrDefaultAsync($"""
            SELECT * FROM {T("EVENEMENT_INSTANCE")}
            WHERE ID_INSTANCE = @IdInstance
              AND TYPE_EVENEMENT = 'NoeudSuspendu'
            ORDER BY ID DESC LIMIT 1
            """, new { IdInstance = idInstance }, transaction);

        return row is null ? null : MapperEvenement(row);
    }

    private static EvenementInstance MapperEvenement(dynamic row) => new()
    {
        Id = (long)row.ID,
        IdInstance = (long)row.ID_INSTANCE,
        TypeEvenement = Enum.Parse<TypeEvenement>((string)row.TYPE_EVENEMENT),
        IdNoeud = row.ID_NOEUD,
        NomNoeud = row.NOM_NOEUD,
        Horodatage = DateTime.Parse((string)row.HORODATAGE),
        DureeMs = row.DUREE_MS,
        Resultat = row.RESULTAT is not null
            ? Enum.Parse<ResultatEvenement>((string)row.RESULTAT)
            : null,
        Detail = row.DETAIL
    };
}
