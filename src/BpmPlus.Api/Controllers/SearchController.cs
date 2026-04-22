using BpmPlus.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace BpmPlus.Api.Controllers;

/// <summary>
/// Recherche multi-critères paginée sur les instances de processus.
/// Supporte de gros volumes via SQL dynamique côté serveur.
/// </summary>
[ApiController]
[Route("api/instances/recherche")]
public class SearchController : ControllerBase
{
    private readonly InstanceSearchService _search;

    public SearchController(InstanceSearchService search) => _search = search;

    /// <summary>
    /// Recherche avancée paginée.
    /// Les paramètres sont passés en query string (GET) ou en corps JSON (POST).
    /// </summary>
    [HttpGet]
    public Task<IActionResult> Get([FromQuery] RechercheInstancesQuery q, CancellationToken ct)
        => Executer(q, ct);

    [HttpPost]
    public Task<IActionResult> Post([FromBody] RechercheInstancesQuery q, CancellationToken ct)
        => Executer(q, ct);

    private async Task<IActionResult> Executer(RechercheInstancesQuery q, CancellationToken ct)
    {
        var resultat = await _search.RechercherAsync(q, ct);
        return Ok(new
        {
            resultat.Total,
            resultat.Page,
            resultat.Taille,
            TotalPages = (long)Math.Ceiling((double)resultat.Total / resultat.Taille),
            Instances  = resultat.Instances
        });
    }
}
