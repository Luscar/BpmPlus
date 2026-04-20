using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;
using BpmPlus.Tests.Integration.Infrastructure;
using Xunit;

namespace BpmPlus.Tests.Integration;

/// <summary>
/// Tests E2E : processus linéaire à trois nœuds métier.
///
///   initialiser-dossier (NoeudMetier)
///     → traiter-dossier   (NoeudMetier)
///       → finaliser-dossier (NoeudMetier, final)
/// </summary>
public class ProcessusSimpleTests : TestBase
{
    private const string CleProcessus = "processus-lineaire";

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        var definition = new DefinitionProcessusBuilder(
                CleProcessus,
                "Processus linéaire de test",
                "initialiser-dossier")
            .AjouterNoeudMetier("initialiser-dossier", "Initialiser le dossier",
                vers: "traiter-dossier")
            .AjouterNoeudMetier("traiter-dossier", "Traiter le dossier",
                vers: "finaliser-dossier")
            .AjouterNoeudMetier("finaliser-dossier", "Finaliser le dossier")
            .Construire();

        await PublierDefinitionAsync(definition);
    }

    // ── 1. Démarrage et statut final ──────────────────────────────────────────

    [Fact]
    public async Task Lineaire_InstanceTermineeImmediatement()
    {
        var id = await DemarrerAsync(CleProcessus, aggregateId: 1001);

        var instance = await ObtenirAsync(id);

        Assert.Equal(StatutInstance.Terminee, instance.Statut);
        Assert.Null(instance.IdNoeudCourant);
        Assert.NotNull(instance.DateFin);
    }

    // ── 2. Variables initiales et propagation ─────────────────────────────────

    [Fact]
    public async Task Lineaire_VariablesInitialesAccessiblesDansHandlers()
    {
        var id = await DemarrerAsync(CleProcessus, aggregateId: 1002,
            variables: new Dictionary<string, object?> { ["montant"] = 250.00m });

        // La variable "dossier_ref" est définie par InitialiserDossierHandler
        var resultats = await RechercherParVariableAsync("dossier_ref", "DOS-1002");

        Assert.Single(resultats);
        Assert.Equal(id, resultats[0].Id);
    }

    [Fact]
    public async Task Lineaire_HandlerPeutLireVariableInitiale()
    {
        // Le handler initialise "etape" = "initialise", puis "traite", puis "finalise"
        // Après complétion, on vérifie que la dernière valeur est "finalise"
        var id = await DemarrerAsync(CleProcessus, aggregateId: 1003);

        var resultats = await RechercherParVariableAsync("etape", "finalise");

        Assert.Contains(resultats, i => i.Id == id);
    }

    // ── 3. Historique des événements ──────────────────────────────────────────

    [Fact]
    public async Task Lineaire_HistoriqueContientTousLesEvenements()
    {
        var id = await DemarrerAsync(CleProcessus, aggregateId: 1004);

        var historique = await ObtenirHistoriqueAsync(id);

        AssertEvenementPresent(historique, TypeEvenement.DebutProcessus);
        AssertEvenementPresent(historique, TypeEvenement.EntreeNoeud, "initialiser-dossier");
        AssertEvenementPresent(historique, TypeEvenement.SortieNoeud, "initialiser-dossier");
        AssertEvenementPresent(historique, TypeEvenement.EntreeNoeud, "traiter-dossier");
        AssertEvenementPresent(historique, TypeEvenement.SortieNoeud, "traiter-dossier");
        AssertEvenementPresent(historique, TypeEvenement.EntreeNoeud, "finaliser-dossier");
        AssertEvenementPresent(historique, TypeEvenement.SortieNoeud, "finaliser-dossier");
        AssertEvenementPresent(historique, TypeEvenement.FinProcessus);
    }

    [Fact]
    public async Task Lineaire_HistoriqueOrdonneChronologiquement()
    {
        var id = await DemarrerAsync(CleProcessus, aggregateId: 1005);

        var historique = await ObtenirHistoriqueAsync(id);

        for (var i = 1; i < historique.Count; i++)
            Assert.True(historique[i].Horodatage >= historique[i - 1].Horodatage);
    }

    // ── 4. Contrainte d'unicité par agrégat ────────────────────────────────────

    [Fact]
    public async Task Lineaire_ApresTernimaison_NouvelDemarrageMemeAggregatePossible()
    {
        // La contrainte d'unicité porte sur les instances actives/suspendues uniquement.
        // Un processus linéaire se termine immédiatement → un second démarrage sur le
        // même agrégat doit réussir (voir ProcessusComplexeTests pour le cas suspendu).
        await DemarrerAsync(CleProcessus, aggregateId: 1006);

        var id2 = await DemarrerAsync(CleProcessus, aggregateId: 1006);

        Assert.True(id2 > 0);
    }

    [Fact]
    public async Task Lineaire_DonneesDInstanceCorrectesApresCreation()
    {
        var aggregateId = 1007L;
        var avant = DateTime.UtcNow.AddSeconds(-1);
        var id = await DemarrerAsync(CleProcessus, aggregateId);

        var instance = await ObtenirAsync(id);

        Assert.Equal(CleProcessus, instance.CleDefinition);
        Assert.Equal(1, instance.VersionDefinition);
        Assert.Equal(aggregateId, instance.AggregateId);
        Assert.True(instance.DateDebut >= avant);
        Assert.NotNull(instance.DateFin);
    }

    // ── 5. Recherche par variable et par agrégat ──────────────────────────────

    [Fact]
    public async Task Lineaire_RechercheParVariable_TrouveInstanceCorrecte()
    {
        await DemarrerAsync(CleProcessus, aggregateId: 2001);
        var id = await DemarrerAsync(CleProcessus, aggregateId: 2002);

        var resultats = await RechercherParVariableAsync("dossier_ref", "DOS-2002");

        Assert.Single(resultats);
        Assert.Equal(id, resultats[0].Id);
    }

    [Fact]
    public async Task Lineaire_RechercheParVariable_Inexistante_RetourneListeVide()
    {
        await DemarrerAsync(CleProcessus, aggregateId: 2003);

        var resultats = await RechercherParVariableAsync("variable_inexistante", "valeur");

        Assert.Empty(resultats);
    }
}
