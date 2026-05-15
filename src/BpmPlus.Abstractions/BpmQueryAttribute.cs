namespace BpmPlus.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BpmQueryAttribute(string nom) : Attribute
{
    public string Nom { get; } = nom;
}
