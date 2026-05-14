using BpmPlus.Abstractions;
using BpmPlus.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BpmPlus.IntegrationTests.Repository;

/// <summary>
/// Tests d'intégration des opérations CRUD sur les variables de processus
/// via le repository SQLite, incluant les valeurs null.
/// </summary>
public class RepositoryVariableTests : IDisposable
{
    private readonly BpmFixture _fixture = new();
    private long _idInstance;

    public RepositoryVariableTests()
    {
        var instance = new InstanceProcessus
        {
            CleDefinition = "proc-var-test",
            VersionDefinition = 1,
            AggregateId = 999,
            Statut = StatutInstance.Active,
            DateDebut = DateTime.UtcNow,
            DateCreation = DateTime.UtcNow,
            DateMaj = DateTime.UtcNow
        };
        _idInstance = _fixture.RepoInstance.CreerAsync(instance).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task SauvegarderToutes_AvecValeurNull_ChargeCorrctement()
    {
        var vars = new Dictionary<string, object?> { ["reference"] = null };

        await _fixture.RepoVariable.SauvegarderToutesAsync(_idInstance, vars);

        var chargees = await _fixture.RepoVariable.ChargerToutesAsync(_idInstance);
        chargees.Should().ContainKey("reference");
        chargees["reference"].Should().BeNull();
    }

    [Fact]
    public async Task SauvegarderToutes_MixteNullEtValeurs_ToutesChargeesFidelement()
    {
        var vars = new Dictionary<string, object?>
        {
            ["texte"]   = "bonjour",
            ["nombre"]  = 42,
            ["nul"]     = null,
            ["montant"] = 99.5m,
            ["flag"]    = true,
            ["date"]    = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        await _fixture.RepoVariable.SauvegarderToutesAsync(_idInstance, vars);

        var chargees = await _fixture.RepoVariable.ChargerToutesAsync(_idInstance);
        chargees.Should().HaveCount(6);
        chargees["texte"].Should().Be("bonjour");
        chargees["nombre"].Should().Be(42L);
        chargees["nul"].Should().BeNull();
        chargees["montant"].Should().Be(99.5m);
        chargees["flag"].Should().Be(true);
        chargees["date"].Should().Be(new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task MettreAJour_ValeurNull_Persiste()
    {
        await _fixture.RepoVariable.MettreAJourAsync(_idInstance, "x", "initial");

        await _fixture.RepoVariable.MettreAJourAsync(_idInstance, "x", null);

        var chargees = await _fixture.RepoVariable.ChargerToutesAsync(_idInstance);
        chargees.Should().ContainKey("x");
        chargees["x"].Should().BeNull();
    }

    [Fact]
    public async Task MettreAJour_NullPuisValeur_EcraseCorrctement()
    {
        await _fixture.RepoVariable.MettreAJourAsync(_idInstance, "y", null);
        await _fixture.RepoVariable.MettreAJourAsync(_idInstance, "y", "final");

        var chargees = await _fixture.RepoVariable.ChargerToutesAsync(_idInstance);
        chargees["y"].Should().Be("final");
    }

    [Fact]
    public async Task ChargerToutes_AucuneVariable_RetourneDictionnaireVide()
    {
        var chargees = await _fixture.RepoVariable.ChargerToutesAsync(_idInstance);
        chargees.Should().BeEmpty();
    }

    [Fact]
    public async Task SauvegarderToutes_EcraseLesVariablesExistantes()
    {
        await _fixture.RepoVariable.SauvegarderToutesAsync(_idInstance,
            new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 });

        await _fixture.RepoVariable.SauvegarderToutesAsync(_idInstance,
            new Dictionary<string, object?> { ["c"] = null });

        var chargees = await _fixture.RepoVariable.ChargerToutesAsync(_idInstance);
        chargees.Should().HaveCount(1);
        chargees.Should().ContainKey("c");
        chargees["c"].Should().BeNull();
    }

    public void Dispose() => _fixture.Dispose();
}
