namespace BpmPlus.Abstractions;

public record ResultatMigration(
    long IdInstance,
    bool Succes,
    int AncienneVersion,
    int NouvelleVersion,
    string? AncienNoeudId,
    string? NouveauNoeudId,
    string? MessageErreur
);
