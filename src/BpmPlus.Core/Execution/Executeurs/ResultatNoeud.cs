namespace BpmPlus.Core.Execution.Executeurs;

public enum TypeResultatNoeud
{
    Suivant,
    Suspendu,
    Termine
}

public record ResultatNoeud(
    TypeResultatNoeud Type,
    string? NoeudSuivantId,
    string? Detail = null
);
