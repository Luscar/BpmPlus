using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Microsoft.AspNetCore.Mvc;

namespace BpmPlus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IServiceBpm _bpm;
    private readonly IRepositoryInstance _repoInstance;

    public DashboardController(IServiceBpm bpm, IRepositoryInstance repoInstance)
    {
        _bpm = bpm;
        _repoInstance = repoInstance;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var definitions = await _bpm.ObtenirDefinitionsAsync(ct);
        var actives     = await _repoInstance.ObtenirParStatutAsync(StatutInstance.Active, ct);
        var suspendues  = await _repoInstance.ObtenirParStatutAsync(StatutInstance.Suspendue, ct);
        var erreurs     = await _repoInstance.ObtenirParStatutAsync(StatutInstance.EnErreur, ct);
        var terminees   = await _repoInstance.ObtenirParStatutAsync(StatutInstance.Terminee, ct);
        var echues      = await _bpm.ObtenirInstancesEchuesAsync(DateTime.UtcNow, ct);

        return Ok(new
        {
            definitions = new
            {
                total     = definitions.Count,
                publiees  = definitions.Count(d => d.Statut == StatutDefinition.Publiee),
                brouillons = definitions.Count(d => d.Statut == StatutDefinition.Brouillon)
            },
            instances = new
            {
                actives    = actives.Count,
                suspendues = suspendues.Count,
                enErreur   = erreurs.Count,
                terminees  = terminees.Count,
                echues     = echues.Count,
                total      = actives.Count + suspendues.Count + erreurs.Count + terminees.Count
            }
        });
    }
}
