using BpmPlus.Core.Execution;
using FluentAssertions;
using Xunit;

namespace BpmPlus.UnitTests.Execution;

public class AccesseurVariablesTests
{
    [Fact]
    public void Obtenir_WhenVariableExists_ReturnsValue()
    {
        var acc = new AccesseurVariables(new Dictionary<string, object?> { ["age"] = 42 });
        acc.Obtenir<int>("age").Should().Be(42);
    }

    [Fact]
    public void Obtenir_WhenAbsent_ThrowsKeyNotFoundException()
    {
        var acc = new AccesseurVariables(new Dictionary<string, object?>());
        var act = () => acc.Obtenir<string>("absent");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void ObtenirOuDefaut_WhenAbsent_ReturnsDefault()
    {
        var acc = new AccesseurVariables(new Dictionary<string, object?>());
        acc.ObtenirOuDefaut<int>("x").Should().Be(0);
        acc.ObtenirOuDefaut<string>("y").Should().BeNull();
    }

    [Fact]
    public void Definir_SetsEstModifie()
    {
        var acc = new AccesseurVariables(new Dictionary<string, object?>());
        acc.EstModifie.Should().BeFalse();
        acc.Definir("x", 10);
        acc.EstModifie.Should().BeTrue();
    }

    [Fact]
    public void Definir_OverwritesExistingVariable()
    {
        var acc = new AccesseurVariables(new Dictionary<string, object?> { ["x"] = 1 });
        acc.Definir("x", 99);
        acc.Obtenir<int>("x").Should().Be(99);
    }

    [Fact]
    public void Existe_ReturnsTrueWhenPresent_FalseWhenAbsent()
    {
        var acc = new AccesseurVariables(new Dictionary<string, object?> { ["y"] = "hello" });
        acc.Existe("y").Should().BeTrue();
        acc.Existe("z").Should().BeFalse();
    }

    [Fact]
    public void ObtenirToutes_ReturnsAllVariables()
    {
        var vars = new Dictionary<string, object?> { ["a"] = 1, ["b"] = "deux" };
        var acc = new AccesseurVariables(vars);
        acc.ObtenirToutes().Should().HaveCount(2)
            .And.ContainKey("a")
            .And.ContainKey("b");
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("100", 100)]
    public void Obtenir_ConvertsStringToInt(string raw, int expected)
    {
        var acc = new AccesseurVariables(new Dictionary<string, object?> { ["v"] = raw });
        acc.Obtenir<int>("v").Should().Be(expected);
    }

    [Fact]
    public void Obtenir_WhenNullAndNonNullableType_ThrowsInvalidCast()
    {
        var acc = new AccesseurVariables(new Dictionary<string, object?> { ["x"] = null });
        var act = () => acc.Obtenir<int>("x");
        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void Obtenir_WhenNullAndNullableType_ReturnsNull()
    {
        var acc = new AccesseurVariables(new Dictionary<string, object?> { ["x"] = null });
        acc.Obtenir<string?>("x").Should().BeNull();
    }

    [Fact]
    public void Definir_NullValue_PreservesKey()
    {
        var acc = new AccesseurVariables(new Dictionary<string, object?>());
        acc.Definir("x", null);
        acc.Existe("x").Should().BeTrue();
        acc.ObtenirOuDefaut<string>("x").Should().BeNull();
    }
}
