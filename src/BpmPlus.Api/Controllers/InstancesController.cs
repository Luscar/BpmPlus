using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Microsoft.AspNetCore.Mvc;

namespace BpmPlus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InstancesController : ControllerBase
{
    private readonly IServiceBpm _bpm;
    private readonly IRepositoryVariable _repoVariable;
    private readonly IRepositoryInstance _repoInstance;

    public InstancesController(
        IServiceBpm bpm,
        IRepositoryVariable repoVariable,
        IRepositoryInstance repoInstance)
    {
        _bpm = bpm;
        _repoVariable = repoVariable;
        _repoInstance = repoInstance;
    }

    // ── Liste ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Lister(
        [FromQuery] string? statut,
        [FromQuery] string? cleDefinition,
        CancellationToken ct)
    {
        IReadOnlyList<InstanceProcessus> instances;

        if (Enum.TryParse<StatutInstance>(statut, true, out var statutParsed))
        {
            instances = await _repoInstance.ObtenirParStatutAsync(statutParsed, ct);
        }
        else
        {
            // Retourne toutes les instances en combinant les statuts connus
            var actives    = await _repoInstance.ObtenirParStatutAsync(StatutInstance.Active, ct);
            var suspendues = await _repoInstance.ObtenirParStatutAsync(StatutInstance.Suspendue, ct);
            var erreurs    = await _repoInstance.ObtenirParStatutAsync(StatutInstance.EnErreur, ct);
            var terminees  = await _repoInstance.ObtenirParStatutAsync(StatutInstance.Terminee, ct);
            instances = actives.Concat(suspendues).Concat(erreurs).Concat(terminees).ToList();
        }

        if (!string.IsNullOrEmpty(cleDefinition))
            instances = instances.Where(i => i.CleDefinition == cleDefinition).ToList();

        return Ok(instances.OrderByDescending(i => i.DateCreation));
    }

    [HttpGet("echues")]
    public async Task<IActionResult> ObtenirEchues(CancellationToken ct)
    {
        var echues = await _bpm.ObtenirInstancesEchuesAsync(DateTime.UtcNow, ct);
        return Ok(echues);
    }

    // ── Détail ────────────────────────────────────────────────────────────────

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Obtenir(long id, CancellationToken ct)
    {
        try
        {
            var instance = await _bpm.ObtenirAsync(id, ct);
            return Ok(instance);
        }
        catch (Exception ex) when (ex.Message.Contains("introuvable") || ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch
        {
            return NotFound();
        }
    }

    [HttpGet("{id:long}/historique")]
    public async Task<IActionResult> ObtenirHistorique(long id, CancellationToken ct)
    {
        var historique = await _bpm.ObtenirHistoriqueAsync(id, ct);
        return Ok(historique);
    }

    [HttpGet("{id:long}/variables")]
    public async Task<IActionResult> ObtenirVariables(long id, CancellationToken ct)
    {
        var variables = await _repoVariable.ChargerToutesAsync(id, ct);
        return Ok(variables);
    }

    [HttpGet("{id:long}/enfants")]
    public async Task<IActionResult> ObtenirEnfants(long id, CancellationToken ct)
    {
        var enfants = await _bpm.ObtenirEnfantsAsync(id, ct);
        return Ok(enfants);
    }

    [HttpGet("{id:long}/signaux")]
    public async Task<IActionResult> ObtenirSignaux(long id, CancellationToken ct)
    {
        var signaux = await _bpm.ObtenirSignauxEnAttenteAsync(id, ct);
        return Ok(signaux);
    }

    [HttpGet("{id:long}/tache")]
    public async Task<IActionResult> ObtenirTache(long id, CancellationToken ct)
    {
        var idTache = await _bpm.ObtenirIdTacheActiveAsync(id, ct);
        var logon   = await _bpm.ObtenirLogonTacheActiveAsync(id, ct);
        return Ok(new { idTache, logon });
    }

    // ── Démarrer ──────────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> Demarrer(
        [FromBody] DemarrerInstanceRequest req,
        CancellationToken ct)
    {
        try
        {
            var id = await _bpm.DemarrerAsync(
                req.CleDefinition, req.AggregateId, req.Variables, ct);
            return CreatedAtAction(nameof(Obtenir), new { id }, new { id });
        }
        catch (Exception ex)
        {
            return BadRequest(new { erreur = ex.Message });
        }
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    [HttpPost("{id:long}/terminer-etape")]
    public async Task<IActionResult> TerminerEtape(long id, CancellationToken ct)
    {
        try
        {
            await _bpm.TerminerEtapeAsync(id, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { erreur = ex.Message });
        }
    }

    [HttpPost("{id:long}/signal")]
    public async Task<IActionResult> EnvoyerSignal(
        long id,
        [FromBody] EnvoyerSignalRequest req,
        CancellationToken ct)
    {
        try
        {
            await _bpm.EnvoyerSignalAsync(req.NomSignal, id, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { erreur = ex.Message });
        }
    }

    [HttpPost("{id:long}/reprendre-timer")]
    public async Task<IActionResult> ReprendreTimer(long id, CancellationToken ct)
    {
        try
        {
            await _bpm.ReprendreAttenteTempsAsync(id, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { erreur = ex.Message });
        }
    }

    [HttpPost("{id:long}/assigner")]
    public async Task<IActionResult> Assigner(
        long id,
        [FromBody] AssignerRequest req,
        CancellationToken ct)
    {
        try
        {
            await _bpm.AssignerLogonAsync(id, req.Logon, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { erreur = ex.Message });
        }
    }

    [HttpPut("{id:long}/variables/{nom}")]
    public async Task<IActionResult> ModifierVariable(
        long id, string nom,
        [FromBody] ModifierVariableRequest req,
        CancellationToken ct)
    {
        try
        {
            await _bpm.ModifierVariableAsync(id, nom, req.Valeur, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { erreur = ex.Message });
        }
    }
}

public record DemarrerInstanceRequest(
    string CleDefinition,
    long AggregateId,
    Dictionary<string, object?>? Variables);

public record EnvoyerSignalRequest(string NomSignal);
public record AssignerRequest(string Logon);
public record ModifierVariableRequest(object? Valeur);
