namespace BpmPlus.Abstractions;

public class InstanceProcessus
{
    public long Id { get; set; }
    public string CleDefinition { get; set; } = string.Empty;
    public int VersionDefinition { get; set; }
    public long AggregateId { get; set; }
    public StatutInstance Statut { get; set; }
    public string? IdNoeudCourant { get; set; }
    public long? IdInstanceParent { get; set; }
    public DateTime DateDebut { get; set; }
    public DateTime? DateFin { get; set; }
    public DateTime DateCreation { get; set; }
    public DateTime DateMaj { get; set; }
}
