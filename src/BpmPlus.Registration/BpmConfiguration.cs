using System.Reflection;

namespace BpmPlus.Registration;

public enum BackendPersistance { Sqlite, Oracle }

public class BpmConfiguration
{
    internal List<Assembly> AssembliesHandlers { get; } = new();
    internal Type? TypeGestionTache { get; private set; }
    internal BackendPersistance Backend { get; private set; } = BackendPersistance.Sqlite;
    internal string Prefixe { get; private set; } = "BPM";

    /// <summary>
    /// Scanne l'assembly fournie pour découvrir automatiquement tous les IHandlerCommande
    /// et IHandlerQuery&lt;T&gt; présents.
    /// </summary>
    public BpmConfiguration ScanHandlers(Assembly assembly)
    {
        AssembliesHandlers.Add(assembly);
        return this;
    }

    /// <summary>Enregistre l'implémentation de IGestionTache fournie par l'application cliente.</summary>
    public BpmConfiguration UseGestionTache<T>() where T : BpmPlus.Abstractions.IGestionTache
    {
        TypeGestionTache = typeof(T);
        return this;
    }

    /// <summary>Utilise la persistance Oracle avec le préfixe de tables fourni.</summary>
    public BpmConfiguration UseOracle(string prefixe = "BPM")
    {
        Backend = BackendPersistance.Oracle;
        Prefixe = prefixe.ToUpperInvariant();
        return this;
    }

    /// <summary>Utilise la persistance SQLite avec le préfixe de tables fourni (par défaut pour les tests).</summary>
    public BpmConfiguration UseSqlite(string prefixe = "BPM")
    {
        Backend = BackendPersistance.Sqlite;
        Prefixe = prefixe.ToUpperInvariant();
        return this;
    }
}
