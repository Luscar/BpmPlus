using System.Data;
using System.Text;
using BpmPlus.Abstractions;
using Dapper;

namespace BpmPlus.Api.Infrastructure;

/// <summary>
/// Moteur de recherche multi-critères et paginée sur BPM_INSTANCE_PROCESSUS.
/// Construit une requête SQL dynamique selon les filtres fournis.
/// </summary>
public class InstanceSearchService
{
    private readonly IDbConnection _cn;
    private readonly string _prefixe;

    private string Table => $"{_prefixe}_INSTANCE_PROCESSUS";
    private string TableVariable => $"{_prefixe}_VARIABLE_PROCESSUS";

    public InstanceSearchService(IDbConnection cn, string prefixe)
    {
        _cn = cn;
        _prefixe = prefixe.TrimEnd('_').ToUpperInvariant();
    }

    public async Task<ResultatRecherche> RechercherAsync(
        RechercheInstancesQuery q,
        CancellationToken ct = default)
    {
        var (where, param) = ConstruireWhere(q);

        var countSql = $"SELECT COUNT(*) FROM {Table} i {where}";
        var total = await _cn.QuerySingleAsync<long>(countSql, param);

        if (total == 0)
            return new ResultatRecherche(0, q.Page, q.Taille, []);

        var orderBy = ColonneSort(q.TriColonne, q.TriSens);
        var offset  = (q.Page - 1) * q.Taille;

        var dataSql = $"""
            SELECT i.* FROM {Table} i {where}
            ORDER BY {orderBy}
            LIMIT @Taille OFFSET @Offset
            """;

        ((DynamicParameters)param).Add("Taille", q.Taille);
        ((DynamicParameters)param).Add("Offset", offset);

        var rows = await _cn.QueryAsync(dataSql, param);
        var instances = rows.Select(MapperInstance).ToList();
        return new ResultatRecherche(total, q.Page, q.Taille, instances);
    }

    private (string Where, DynamicParameters Params) ConstruireWhere(RechercheInstancesQuery q)
    {
        var clauses = new List<string>();
        var p = new DynamicParameters();

        // ── Statuts (liste multi-valeur) ──────────────────────────────────────
        if (q.Statuts is { Count: > 0 })
        {
            // Dapper supporte les listes pour IN (...)
            clauses.Add("i.STATUT IN @Statuts");
            p.Add("Statuts", q.Statuts);
        }

        // ── Clé de définition (préfixe insensible à la casse) ─────────────────
        if (!string.IsNullOrWhiteSpace(q.CleDefinition))
        {
            clauses.Add("LOWER(i.CLE_DEFINITION) LIKE LOWER(@CleDefinition)");
            p.Add("CleDefinition", $"%{q.CleDefinition.Trim()}%");
        }

        // ── AggregateId exact ─────────────────────────────────────────────────
        if (q.AggregateId.HasValue)
        {
            clauses.Add("i.AGGREGATE_ID = @AggregateId");
            p.Add("AggregateId", q.AggregateId.Value);
        }

        // ── Nœud courant (contient) ───────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(q.IdNoeudCourant))
        {
            clauses.Add("i.ID_NOEUD_COURANT LIKE @NoeudCourant");
            p.Add("NoeudCourant", $"%{q.IdNoeudCourant.Trim()}%");
        }

        // ── Plage de dates de début ───────────────────────────────────────────
        if (q.DateDebutMin.HasValue)
        {
            clauses.Add("i.DATE_DEBUT >= @DateDebutMin");
            p.Add("DateDebutMin", q.DateDebutMin.Value.ToUniversalTime().ToString("O"));
        }
        if (q.DateDebutMax.HasValue)
        {
            clauses.Add("i.DATE_DEBUT <= @DateDebutMax");
            p.Add("DateDebutMax", q.DateDebutMax.Value.ToUniversalTime().AddDays(1).ToString("O"));
        }

        // ── Racines seulement (pas de parent) ────────────────────────────────
        if (q.RacinesSeulement == true)
            clauses.Add("i.ID_INSTANCE_PARENT IS NULL");

        // ── Recherche sur variable ────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(q.NomVariable) && !string.IsNullOrWhiteSpace(q.ValeurVariable))
        {
            clauses.Add($"""
                EXISTS (
                    SELECT 1 FROM {TableVariable} v
                    WHERE v.ID_INSTANCE = i.ID
                      AND v.NOM = @NomVariable
                      AND v.VALEUR LIKE @ValeurVariable
                )
                """);
            p.Add("NomVariable", q.NomVariable.Trim());
            p.Add("ValeurVariable", $"%{q.ValeurVariable.Trim()}%");
        }

        var where = clauses.Count > 0
            ? "WHERE " + string.Join(" AND ", clauses)
            : string.Empty;

        return (where, p);
    }

    private static string ColonneSort(string? col, string? sens)
    {
        var colonne = col?.ToLowerInvariant() switch
        {
            "id"            => "i.ID",
            "datedebut"     => "i.DATE_DEBUT",
            "datemaj"       => "i.DATE_MAJ",
            "statut"        => "i.STATUT",
            "cledefinition" => "i.CLE_DEFINITION",
            "aggregateid"   => "i.AGGREGATE_ID",
            _               => "i.ID"
        };
        var direction = sens?.ToUpperInvariant() == "ASC" ? "ASC" : "DESC";
        return $"{colonne} {direction}";
    }

    private static InstanceProcessus MapperInstance(dynamic row) => new()
    {
        Id                = (long)row.ID,
        CleDefinition     = (string)row.CLE_DEFINITION,
        VersionDefinition = (int)row.VERSION_DEFINITION,
        AggregateId       = (long)row.AGGREGATE_ID,
        Statut            = Enum.Parse<StatutInstance>((string)row.STATUT),
        IdNoeudCourant    = row.ID_NOEUD_COURANT,
        IdInstanceParent  = row.ID_INSTANCE_PARENT,
        DateDebut         = DateTime.Parse((string)row.DATE_DEBUT),
        DateFin           = row.DATE_FIN is not null ? DateTime.Parse((string)row.DATE_FIN) : null,
        DateCreation      = DateTime.Parse((string)row.DATE_CREATION),
        DateMaj           = DateTime.Parse((string)row.DATE_MAJ)
    };
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class RechercheInstancesQuery
{
    public List<string>? Statuts      { get; set; }
    public string?       CleDefinition { get; set; }
    public long?         AggregateId   { get; set; }
    public string?       IdNoeudCourant { get; set; }
    public DateTime?     DateDebutMin  { get; set; }
    public DateTime?     DateDebutMax  { get; set; }
    public bool?         RacinesSeulement { get; set; }
    public string?       NomVariable   { get; set; }
    public string?       ValeurVariable { get; set; }

    private int _page = 1;
    private int _taille = 25;

    public int Page   { get => _page;   set => _page   = Math.Max(1, value); }
    public int Taille { get => _taille; set => _taille = Math.Clamp(value, 1, 200); }

    public string? TriColonne { get; set; }
    public string? TriSens    { get; set; }
}

public record ResultatRecherche(
    long Total,
    int  Page,
    int  Taille,
    IReadOnlyList<InstanceProcessus> Instances
);
