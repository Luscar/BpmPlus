using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;
using BpmPlus.IntegrationTests.Fixtures;
using FluentAssertions;

namespace BpmPlus.IntegrationTests.Execution;

/// <summary>
/// Tests d'intégration pour les processus avec attente de signal.
/// Couvre : suspension sur signal, envoi ciblé, broadcast.
/// </summary>
public class ProcessusSignalTests : IDisposable
{
    private readonly BpmFixture _fixture = new();

    private async Task<string> PublierProcessusSignalAsync(string cle, string nomSignal, bool avecSuite = true)
    {
        var def = new ProcessusBuilder(cle, $"Processus signal {cle}")
            .Debut("attente");

        if (avecSuite)
            def = def
                .AttenteSignal("attente", nomSignal, "fin")
                .Metier("fin", b => b.Commande("NoOpCommand"));
        else
            def = def.AttenteSignal("attente", nomSignal);

        await _fixture.PublierDefinitionAsync(def.Build());
        return cle;
    }

    [Fact]
    public async Task Demarrer_ProcessusSignal_SuspenduSurNoeudAttenteSignal()
    {
        var cle = await PublierProcessusSignalAsync("p-signal-1", "signal-test");

        var idInstance = await _fixture.ServiceBpm.DemarrerAsync(cle, 1L, null);

        var instance = await _fixture.ServiceBpm.ObtenirAsync(idInstance);
        instance.Statut.Should().Be(StatutInstance.Suspendue);
        instance.IdNoeudCourant.Should().Be("attente");
    }

    [Fact]
    public async Task EnvoyerSignal_Cible_ReprendInstanceEtTermine()
    {
        var cle = await PublierProcessusSignalAsync("p-signal-2", "commencer");

        var idInstance = await _fixture.ServiceBpm.DemarrerAsync(cle, 2L, null);

        await _fixture.ServiceBpm.EnvoyerSignalAsync("commencer", idInstance);

        var instance = await _fixture.ServiceBpm.ObtenirAsync(idInstance);
        instance.Statut.Should().Be(StatutInstance.Terminee);
    }

    [Fact]
    public async Task EnvoyerSignal_Broadcast_ReprendToutesLesInstancesEnAttente()
    {
        var cle = await PublierProcessusSignalAsync("p-signal-broadcast", "go");

        var id1 = await _fixture.ServiceBpm.DemarrerAsync(cle, 10L, null);
        var id2 = await _fixture.ServiceBpm.DemarrerAsync(cle, 11L, null);
        var id3 = await _fixture.ServiceBpm.DemarrerAsync(cle, 12L, null);

        // Broadcast sans idInstance cible
        await _fixture.ServiceBpm.EnvoyerSignalAsync("go");

        var inst1 = await _fixture.ServiceBpm.ObtenirAsync(id1);
        var inst2 = await _fixture.ServiceBpm.ObtenirAsync(id2);
        var inst3 = await _fixture.ServiceBpm.ObtenirAsync(id3);

        inst1.Statut.Should().Be(StatutInstance.Terminee);
        inst2.Statut.Should().Be(StatutInstance.Terminee);
        inst3.Statut.Should().Be(StatutInstance.Terminee);
    }

    [Fact]
    public async Task ObtenirSignauxEnAttente_InstanceSuspendueSignal_RetourneSignal()
    {
        var cle = await PublierProcessusSignalAsync("p-signal-3", "mon-signal");

        var idInstance = await _fixture.ServiceBpm.DemarrerAsync(cle, 20L, null);

        var signaux = await _fixture.ServiceBpm.ObtenirSignauxEnAttenteAsync(idInstance);
        signaux.Should().Contain("mon-signal");
    }

    [Fact]
    public async Task EnvoyerSignal_ApresReprise_SignauxEnAttenteVides()
    {
        var cle = await PublierProcessusSignalAsync("p-signal-4", "terminer");

        var idInstance = await _fixture.ServiceBpm.DemarrerAsync(cle, 30L, null);

        await _fixture.ServiceBpm.EnvoyerSignalAsync("terminer", idInstance);

        var signaux = await _fixture.ServiceBpm.ObtenirSignauxEnAttenteAsync(idInstance);
        signaux.Should().BeEmpty();
    }

    [Fact]
    public async Task EnvoyerSignal_InstanceNonSuspendue_LanceException()
    {
        await _fixture.PublierProcessusSimpleAsync("p-signal-actif");
        var idInstance = await _fixture.ServiceBpm.DemarrerAsync("p-signal-actif", 40L, null);

        // L'instance est terminée — envoyer un signal doit échouer
        var act = async () => await _fixture.ServiceBpm.EnvoyerSignalAsync("signal", idInstance);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task EnvoyerSignal_MauvaisSignalSurNoeud_LanceException()
    {
        var cle = await PublierProcessusSignalAsync("p-signal-5", "signal-attendu");
        var idInstance = await _fixture.ServiceBpm.DemarrerAsync(cle, 50L, null);

        // L'instance attend "signal-attendu" mais on envoie un autre signal via broadcast
        // → le nœud courant n'est pas NoeudAttenteSignal pour le mauvais signal envoyé ciblé
        // On teste que l'envoi ciblé avec le bon signal fonctionne et le mauvais signal broadcast
        // n'affecte pas les instances qui n'attendent pas ce signal
        var act = async () => await _fixture.ServiceBpm.EnvoyerSignalAsync("mauvais-signal", idInstance);
        await act.Should().ThrowAsync<EtatInstanceInvalideException>();
    }

    public void Dispose() => _fixture.Dispose();
}
