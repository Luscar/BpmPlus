namespace BpmPlus.Abstractions;

public class DefinitionTache
{
    public string Titre { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Categorie { get; set; }
    public string? LogonAuto { get; set; }

    /// <summary>Code de rôle requis pour cette tâche (ex. "RESPONSABLE", "VALIDATEUR").</summary>
    public string? CodeRole { get; set; }

    /// <summary>Code identifiant le type de tâche dans le système externe.</summary>
    public string? CodeTache { get; set; }

    /// <summary>Nom du nœud interactif dans la définition du processus. Renseigné automatiquement par le moteur.</summary>
    public string? NomNoeud { get; set; }

    /// <summary>Indique que la tâche est une révision d'une tâche existante.</summary>
    public bool IndTacheRevision { get; set; }

    /// <summary>Logon de l'auteur ou du créateur de l'élément soumis à la tâche.</summary>
    public string? LogonAuteur { get; set; }

    public IReadOnlyDictionary<string, object?> MetaDonnees { get; set; }
        = new Dictionary<string, object?>();
}
