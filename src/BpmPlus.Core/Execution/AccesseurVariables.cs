using BpmPlus.Abstractions;

namespace BpmPlus.Core.Execution;

public class AccesseurVariables : IAccesseurVariables
{
    private readonly Dictionary<string, object?> _variables;
    private bool _modifie;

    public AccesseurVariables(Dictionary<string, object?> variables)
    {
        _variables = variables;
    }

    public bool EstModifie => _modifie;

    public T Obtenir<T>(string nom)
    {
        if (!_variables.TryGetValue(nom, out var valeur))
            throw new KeyNotFoundException($"Variable '{nom}' introuvable dans l'instance.");
        return ConvertirValeur<T>(valeur);
    }

    public T? ObtenirOuDefaut<T>(string nom)
    {
        if (!_variables.TryGetValue(nom, out var valeur))
            return default;
        return ConvertirValeur<T>(valeur);
    }

    public void Definir(string nom, object? valeur)
    {
        _variables[nom] = valeur;
        _modifie = true;
    }

    public bool Existe(string nom) => _variables.ContainsKey(nom);

    public IReadOnlyDictionary<string, object?> ObtenirToutes() => _variables;

    private static T ConvertirValeur<T>(object? valeur)
    {
        if (valeur is null)
        {
            if (default(T) is null) return default!;
            throw new InvalidCastException($"La variable est nulle et le type {typeof(T).Name} n'accepte pas null.");
        }
        if (valeur is T typed) return typed;

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(valeur, targetType);
    }
}
