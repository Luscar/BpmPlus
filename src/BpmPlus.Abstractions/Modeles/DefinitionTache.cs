namespace BpmPlus.Abstractions;

public class DefinitionTache
{
    public string Titre { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Categorie { get; set; }
    public string? LogonAuto { get; set; }
    public IReadOnlyDictionary<string, object?> MetaDonnees { get; set; }
        = new Dictionary<string, object?>();
}
