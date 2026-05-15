namespace BpmPlus.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BpmCommandeAttribute(string nom) : Attribute
{
    public string Nom { get; } = nom;
}
