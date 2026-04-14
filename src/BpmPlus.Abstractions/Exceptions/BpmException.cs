namespace BpmPlus.Abstractions;

public abstract class BpmException : Exception
{
    protected BpmException(string message) : base(message) { }
    protected BpmException(string message, Exception inner) : base(message, inner) { }
}

public class NoeudIntrouvableException : BpmException
{
    public string IdNoeud { get; }
    public NoeudIntrouvableException(string idNoeud)
        : base($"Le nœud '{idNoeud}' est introuvable dans la définition.")
        => IdNoeud = idNoeud;
}

public class AucunCheminException : BpmException
{
    public string IdNoeudDecision { get; }
    public AucunCheminException(string idNoeudDecision)
        : base($"Aucune condition vraie et aucune branche par défaut sur le nœud de décision '{idNoeudDecision}'.")
        => IdNoeudDecision = idNoeudDecision;
}

public class EtatInstanceInvalideException : BpmException
{
    public long IdInstance { get; }
    public StatutInstance StatutActuel { get; }
    public EtatInstanceInvalideException(long idInstance, StatutInstance statutActuel, string message)
        : base(message)
    {
        IdInstance = idInstance;
        StatutActuel = statutActuel;
    }
}

public class MigrationImpossibleException : BpmException
{
    public long IdInstance { get; }
    public string? IdNoeud { get; }
    public MigrationImpossibleException(long idInstance, string? idNoeud, string message)
        : base(message)
    {
        IdInstance = idInstance;
        IdNoeud = idNoeud;
    }
}

public class ProcessusDejaActifException : BpmException
{
    public string CleDefinition { get; }
    public long AggregateId { get; }
    public ProcessusDejaActifException(string cleDefinition, long aggregateId)
        : base($"Un processus actif existe déjà pour la définition '{cleDefinition}' et l'agrégat {aggregateId}.")
    {
        CleDefinition = cleDefinition;
        AggregateId = aggregateId;
    }
}

public class DefinitionIntrouvableException : BpmException
{
    public string CleDefinition { get; }
    public int? Version { get; }
    public DefinitionIntrouvableException(string cleDefinition, int? version = null)
        : base(version.HasValue
            ? $"La définition '{cleDefinition}' version {version} est introuvable."
            : $"La définition '{cleDefinition}' est introuvable.")
    {
        CleDefinition = cleDefinition;
        Version = version;
    }
}

public class DefinitionImmuableException : BpmException
{
    public DefinitionImmuableException(string cleDefinition)
        : base($"La définition '{cleDefinition}' est publiée et immuable.") { }
}
