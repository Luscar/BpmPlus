namespace BpmPlus.Abstractions;

public class DefinitionTache
{
    public string Titre { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Categorie { get; set; }
    public ISourceParametre? SourceLogonAuto { get; set; }

    /// <summary>Code de rôle requis pour cette tâche (ex. "RESPONSABLE", "VALIDATEUR").</summary>
    public string? CodeRole { get; set; }

    /// <summary>Code identifiant le type de tâche dans le système externe.</summary>
    public string? CodeTache { get; set; }

    /// <summary>Nom du nœud interactif dans la définition du processus. Renseigné automatiquement par le moteur.</summary>
    public string? NomNoeud { get; set; }

    /// <summary>Logon de l'auteur ou du créateur de l'élément soumis à la tâche.</summary>
    public string? LogonAuteur { get; set; }

    /// <summary>Nom de la variable de processus à alimenter avec le logon de l'assigné lors de l'auto-assignation ou de l'assignation manuelle.</summary>
    public string? NomVariableLogonAssigne { get; set; }

    /// <summary>Nom de la variable de processus à alimenter avec le logon de l'assigné courant lors de la complétion de la tâche (utile pour la tâche suivante).</summary>
    public string? NomVariableLogonTachePrecedente { get; set; }

    public IReadOnlyDictionary<string, object?> MetaDonnees { get; set; }
        = new Dictionary<string, object?>();
}
