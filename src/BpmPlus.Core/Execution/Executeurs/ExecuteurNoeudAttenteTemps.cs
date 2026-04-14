using BpmPlus.Abstractions;
using Microsoft.Extensions.Logging;

namespace BpmPlus.Core.Execution.Executeurs;

public class ExecuteurNoeudAttenteTemps
{
    private readonly ResolveurParametre _resolveur;
    private readonly ILogger<ExecuteurNoeudAttenteTemps> _logger;

    public ExecuteurNoeudAttenteTemps(ResolveurParametre resolveur, ILogger<ExecuteurNoeudAttenteTemps> logger)
    {
        _resolveur = resolveur;
        _logger = logger;
    }

    public async Task<ResultatNoeud> EntrerAsync(
        NoeudAttenteTemps noeud,
        IContexteExecution contexte,
        CancellationToken ct)
    {
        var valeurEcheance = await _resolveur.ResolveAsync(noeud.SourceDateEcheance, contexte, ct);
        DateTime dateEcheance;

        if (valeurEcheance is DateTime dt)
            dateEcheance = dt;
        else if (valeurEcheance is string s && DateTime.TryParse(s, out var parsed))
            dateEcheance = parsed;
        else
            throw new InvalidOperationException(
                $"Impossible de résoudre la date d'échéance pour le nœud '{noeud.Id}'. Valeur: {valeurEcheance}");

        _logger.LogInformation("NoeudAttenteTemps '{Id}' — suspension jusqu'au {Date:O}", noeud.Id, dateEcheance);

        var detail = System.Text.Json.JsonSerializer.Serialize(new
        {
            typeAttente = "Temps",
            dateEcheance = dateEcheance.ToString("O"),
            noeudId = noeud.Id
        });

        return new ResultatNoeud(TypeResultatNoeud.Suspendu, null, detail);
    }

    public ResultatNoeud Reprendre(NoeudAttenteTemps noeud)
    {
        _logger.LogInformation("NoeudAttenteTemps '{Id}' — reprise après échéance", noeud.Id);

        if (noeud.EstFinale)
            return new ResultatNoeud(TypeResultatNoeud.Termine, null);

        var suivant = noeud.FluxSortants.FirstOrDefault()?.Vers;
        return new ResultatNoeud(TypeResultatNoeud.Suivant, suivant);
    }
}
