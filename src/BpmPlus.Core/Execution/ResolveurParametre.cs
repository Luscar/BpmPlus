using Autofac;
using BpmPlus.Abstractions;
using Microsoft.Extensions.Logging;

namespace BpmPlus.Core.Execution;

/// <summary>
/// Résout les ISourceParametre et évalue les ICondition en déléguant les queries
/// aux handlers enregistrés dans le conteneur Autofac.
/// </summary>
public class ResolveurParametre
{
    private readonly ILifetimeScope _scope;
    private readonly ILogger<ResolveurParametre> _logger;

    public ResolveurParametre(ILifetimeScope scope, ILogger<ResolveurParametre> logger)
    {
        _scope = scope;
        _logger = logger;
    }

    public async Task<object?> ResolveAsync(
        ISourceParametre source,
        IContexteExecution contexte,
        CancellationToken ct)
    {
        switch (source)
        {
            case SourceVariable sv:
                var val = contexte.Variables.ObtenirOuDefaut<object?>(sv.NomVariable);
                _logger.LogDebug("Résolution SourceVariable '{Nom}' = {Valeur}", sv.NomVariable, val);
                return val;

            case SourceValeurStatique svs:
                _logger.LogDebug("Résolution SourceValeurStatique = {Valeur}", svs.Valeur);
                return svs.Valeur;

            case SourceQuery sq:
                return await ResolveSourceQueryAsync(sq, contexte, ct);

            default:
                throw new InvalidOperationException($"Type de source non supporté : {source.GetType().Name}");
        }
    }

    public async Task<long?> ResolveAggregateIdAsync(
        ISourceParametre? source,
        IContexteExecution contexte,
        CancellationToken ct)
    {
        if (source is null) return null;
        var valeur = await ResolveAsync(source, contexte, ct);
        if (valeur is null) return null;
        return Convert.ToInt64(valeur);
    }

    public async Task<IReadOnlyDictionary<string, object?>> ResolveParametresAsync(
        Dictionary<string, ISourceParametre> sources,
        IContexteExecution contexte,
        CancellationToken ct)
    {
        var result = new Dictionary<string, object?>(sources.Count);
        foreach (var (nom, source) in sources)
            result[nom] = await ResolveAsync(source, contexte, ct);
        return result;
    }

    public async Task<bool> EvaluerConditionAsync(
        ICondition condition,
        IContexteExecution contexte,
        CancellationToken ct)
    {
        switch (condition)
        {
            case ConditionVariable cv:
                return EvaluerConditionVariable(cv, contexte);

            case ConditionQuery cq:
                return await EvaluerConditionQueryAsync(cq, contexte, ct);

            default:
                throw new InvalidOperationException($"Type de condition non supporté : {condition.GetType().Name}");
        }
    }

    private bool EvaluerConditionVariable(ConditionVariable cv, IContexteExecution contexte)
    {
        var valeurInstance = contexte.Variables.ObtenirOuDefaut<object?>(cv.NomVariable);
        _logger.LogDebug("Évaluation condition variable '{Nom}' {Op} '{Valeur}' (actuelle='{Actuelle}')",
            cv.NomVariable, cv.Operateur, cv.Valeur, valeurInstance);

        return cv.Operateur switch
        {
            Operateur.Egal => CompareValeurs(valeurInstance, cv.Valeur) == 0,
            Operateur.Different => CompareValeurs(valeurInstance, cv.Valeur) != 0,
            Operateur.Superieur => CompareValeurs(valeurInstance, cv.Valeur) > 0,
            Operateur.Inferieur => CompareValeurs(valeurInstance, cv.Valeur) < 0,
            Operateur.SuperieurOuEgal => CompareValeurs(valeurInstance, cv.Valeur) >= 0,
            Operateur.InferieurOuEgal => CompareValeurs(valeurInstance, cv.Valeur) <= 0,
            Operateur.Contient => valeurInstance?.ToString()?.Contains(cv.Valeur?.ToString() ?? string.Empty,
                StringComparison.OrdinalIgnoreCase) ?? false,
            _ => throw new InvalidOperationException($"Opérateur non supporté : {cv.Operateur}")
        };
    }

    private static int CompareValeurs(object? a, object? b)
    {
        if (a is null && b is null) return 0;
        if (a is null) return -1;
        if (b is null) return 1;

        // Tenter une comparaison numérique
        if (TryConvertDecimal(a, out var da) && TryConvertDecimal(b, out var db))
            return da.CompareTo(db);

        // Comparaison de dates
        if (a is DateTime dtA && b is DateTime dtB) return dtA.CompareTo(dtB);
        if (DateTime.TryParse(b?.ToString(), out var parsedDate) && a is DateTime dtA2)
            return dtA2.CompareTo(parsedDate);

        // Comparaison de chaînes
        return string.Compare(a.ToString(), b?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryConvertDecimal(object? val, out decimal result)
    {
        result = 0;
        if (val is null) return false;
        if (val is decimal d) { result = d; return true; }
        if (val is int i) { result = i; return true; }
        if (val is long l) { result = l; return true; }
        if (val is double dbl) { result = (decimal)dbl; return true; }
        return decimal.TryParse(val.ToString(), out result);
    }

    private async Task<object?> ResolveSourceQueryAsync(SourceQuery sq, IContexteExecution contexte, CancellationToken ct)
    {
        _logger.LogDebug("Résolution SourceQuery '{NomQuery}'", sq.NomQuery);

        if (!_scope.TryResolveKeyed<IHandlerQuery>(sq.NomQuery, out var handler))
            throw new InvalidOperationException($"Aucun IHandlerQuery enregistré pour la query '{sq.NomQuery}'.");

        var aggregateId = sq.SourceAggregateId != null
            ? await ResolveAggregateIdAsync(sq.SourceAggregateId, contexte, ct)
            : null;

        var parametres = sq.Parametres != null
            ? await ResolveParametresAsync(sq.Parametres, contexte, ct)
            : (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>();

        // Invoquer via réflexion car le type générique est inconnu à la compilation
        var interfaceType = handler.GetType()
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandlerQuery<>));

        if (interfaceType is null)
            throw new InvalidOperationException($"Le handler '{sq.NomQuery}' n'implémente pas IHandlerQuery<T>.");

        var methode = interfaceType.GetMethod("ExecuterAsync")!;
        var tache = (Task)methode.Invoke(handler, [aggregateId, parametres, contexte])!;
        await tache;
        var resultProperty = tache.GetType().GetProperty("Result");
        return resultProperty?.GetValue(tache);
    }

    private async Task<bool> EvaluerConditionQueryAsync(ConditionQuery cq, IContexteExecution contexte, CancellationToken ct)
    {
        _logger.LogDebug("Évaluation ConditionQuery '{NomQuery}'", cq.NomQuery);

        if (!_scope.TryResolveKeyed<IHandlerQuery<bool>>(cq.NomQuery, out var handler))
            throw new InvalidOperationException($"Aucun IHandlerQuery<bool> enregistré pour la query '{cq.NomQuery}'.");

        var aggregateId = cq.SourceAggregateId != null
            ? await ResolveAggregateIdAsync(cq.SourceAggregateId, contexte, ct)
            : null;

        var parametres = cq.Parametres != null
            ? await ResolveParametresAsync(cq.Parametres, contexte, ct)
            : (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>();

        return await handler.ExecuterAsync(aggregateId, parametres, contexte);
    }
}
