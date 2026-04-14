namespace BpmPlus.Abstractions;

public class EvenementInstance
{
    public long Id { get; set; }
    public long IdInstance { get; set; }
    public TypeEvenement TypeEvenement { get; set; }
    public string? IdNoeud { get; set; }
    public string? NomNoeud { get; set; }
    public DateTime Horodatage { get; set; }
    public long? DureeMs { get; set; }
    public ResultatEvenement? Resultat { get; set; }
    public string? Detail { get; set; }
}
