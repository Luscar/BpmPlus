using BpmPlus.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace BpmPlus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DefinitionsController : ControllerBase
{
    private readonly IServiceBpm _bpm;

    public DefinitionsController(IServiceBpm bpm) => _bpm = bpm;

    [HttpGet]
    public async Task<IActionResult> ObtenirTout(CancellationToken ct)
    {
        var definitions = await _bpm.ObtenirDefinitionsAsync(ct);
        return Ok(definitions.Select(d => new
        {
            d.Id,
            d.Cle,
            d.Version,
            d.Nom,
            Statut = d.Statut.ToString(),
            d.DateCreation,
            d.DatePublication,
            NombreNoeuds = d.Noeuds.Count
        }));
    }

    [HttpGet("{cle}")]
    public async Task<IActionResult> ObtenirParCle(string cle, CancellationToken ct)
    {
        var definitions = await _bpm.ObtenirDefinitionsAsync(ct);
        var versions = definitions
            .Where(d => d.Cle == cle)
            .OrderByDescending(d => d.Version)
            .ToList();

        if (!versions.Any())
            return NotFound($"Aucune définition avec la clé '{cle}'.");

        return Ok(versions);
    }

    [HttpGet("{cle}/v/{version:int}")]
    public async Task<IActionResult> ObtenirVersion(string cle, int version, CancellationToken ct)
    {
        var definitions = await _bpm.ObtenirDefinitionsAsync(ct);
        var def = definitions.FirstOrDefault(d => d.Cle == cle && d.Version == version);

        if (def is null)
            return NotFound($"Définition '{cle}' v{version} introuvable.");

        return Ok(def);
    }

    [HttpPost("{cle}/publier")]
    public async Task<IActionResult> Publier(string cle, CancellationToken ct)
    {
        try
        {
            await _bpm.PublierDefinitionAsync(cle, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { erreur = ex.Message });
        }
    }
}
