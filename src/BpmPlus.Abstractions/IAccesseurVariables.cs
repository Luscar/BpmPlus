namespace BpmPlus.Abstractions;

/// <summary>
/// Accès en lecture et en écriture aux variables scalaires d'une instance de processus.
/// Types supportés : string, int, decimal, DateTime, bool.
/// </summary>
public interface IAccesseurVariables
{
    /// <summary>Retourne la valeur typée de la variable. Lance une exception si absente.</summary>
    T Obtenir<T>(string nom);

    /// <summary>Retourne la valeur typée ou la valeur par défaut si absente.</summary>
    T? ObtenirOuDefaut<T>(string nom);

    /// <summary>Définit ou écrase une variable. La valeur doit être un scalaire supporté.</summary>
    void Definir(string nom, object? valeur);

    /// <summary>Indique si une variable existe.</summary>
    bool Existe(string nom);

    /// <summary>Retourne toutes les variables sous forme de dictionnaire.</summary>
    IReadOnlyDictionary<string, object?> ObtenirToutes();
}
