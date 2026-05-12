namespace BpmPlus.Abstractions;

public record FiltreVariable(string NomVariable, object Valeur, Operateur Operateur = Operateur.Egal);
