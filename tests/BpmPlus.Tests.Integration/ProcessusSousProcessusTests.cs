using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;
using BpmPlus.Tests.Integration.Infrastructure;
using Xunit;

namespace BpmPlus.Tests.Integration;

/// <summary>
/// Tests E2E : processus parent avec nœud sous-processus.
///
/// Processus enfant :
///   saisir-informations (NoeudMetier, final)
///
/// Processus parent :
///   initialiser-parent (NoeudMetier)
///     → creer-sous-dossier (NoeudSousProcessus → enfant v1, sorties: ["resultat_enfant"])
///         → finaliser-parent (NoeudMetier, final)
/// </summary>
public class ProcessusSousProcessusTests : TestBase
{
    private const string CleEnfant = "processus-sous-dossier";
    private const string CleParent = "processus-parent";

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await PublierDefinitionAsync(CreerDefinitionEnfant());
        await PublierDefinitionAsync(CreerDefinitionParent());
    }

    private static DefinitionProcessus CreerDefinitionEnfant() =>
        new DefinitionProcessusBuilder(
                CleEnfant,
                "Processus enfant : saisie informations",
                "saisir-informations")
            .AjouterNoeudMetier("saisir-informations", "Saisir les informations")
            .Construire();

    private static DefinitionProcessus CreerDefinitionParent() =>
        new DefinitionProcessusBuilder(
                CleParent,
                "Processus parent",
                "initialiser-parent")
            .AjouterNoeudMetier("initialiser-parent", "Initialiser le parent",
                vers: "creer-sous-dossier")
            .AjouterNoeudSousProcessus("creer-sous-dossier", "Créer le sous-dossier", n => n
                .DefinitionEnfant(CleEnfant, version: 1)
                .SortiesVariables("resultat_enfant", "infos_saisies")
                .Vers("finaliser-parent"))
            .AjouterNoeudMetier("finaliser-parent", "Finaliser le parent")
            .Construire();

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SousProcessus_ParentTermineApresExecution()
    {
        var id = await DemarrerAsync(CleParent, aggregateId: 7001);

        var instance = await ObtenirAsync(id);

        Assert.Equal(StatutInstance.Terminee, instance.Statut);
        Assert.Null(instance.IdNoeudCourant);
    }

    [Fact]
    public async Task SousProcessus_CreUneInstanceEnfant()
    {
        var idParent = await DemarrerAsync(CleParent, aggregateId: 7002);

        var enfants = await ObtenirEnfantsAsync(idParent);

        Assert.Single(enfants);
    }

    [Fact]
    public async Task SousProcessus_InstanceEnfantTerminee()
    {
        var idParent = await DemarrerAsync(CleParent, aggregateId: 7003);

        var enfants = await ObtenirEnfantsAsync(idParent);

        Assert.All(enfants, e => Assert.Equal(StatutInstance.Terminee, e.Statut));
    }

    [Fact]
    public async Task SousProcessus_InstanceEnfantAppartientAuParent()
    {
        var idParent = await DemarrerAsync(CleParent, aggregateId: 7004);

        var enfants = await ObtenirEnfantsAsync(idParent);

        Assert.All(enfants, e => Assert.Equal(idParent, e.IdInstanceParent));
    }

    [Fact]
    public async Task SousProcessus_VariablesSortiesMappeesVersParent()
    {
        var idParent = await DemarrerAsync(CleParent, aggregateId: 7005);

        // La variable "resultat_enfant" est définie par SaisirInformationsHandler
        // et déclarée comme sortie du sous-processus → elle doit être présente dans le parent.
        var resultats = await RechercherParVariableAsync("resultat_enfant", "OK");

        Assert.Contains(resultats, i => i.Id == idParent);
    }

    [Fact]
    public async Task SousProcessus_HistoriqueParentContientNoeudsFinalises()
    {
        var idParent = await DemarrerAsync(CleParent, aggregateId: 7006);

        var historique = await ObtenirHistoriqueAsync(idParent);

        AssertEvenementPresent(historique, TypeEvenement.DebutProcessus);
        AssertEvenementPresent(historique, TypeEvenement.EntreeNoeud, "initialiser-parent");
        AssertEvenementPresent(historique, TypeEvenement.EntreeNoeud, "creer-sous-dossier");
        AssertEvenementPresent(historique, TypeEvenement.SortieNoeud, "creer-sous-dossier");
        AssertEvenementPresent(historique, TypeEvenement.EntreeNoeud, "finaliser-parent");
        AssertEvenementPresent(historique, TypeEvenement.FinProcessus);
    }

    [Fact]
    public async Task SousProcessus_HistoriqueEnfantContientSesNoeuds()
    {
        var idParent = await DemarrerAsync(CleParent, aggregateId: 7007);
        var enfants  = await ObtenirEnfantsAsync(idParent);

        var idEnfant = enfants[0].Id;
        var historique = await ObtenirHistoriqueAsync(idEnfant);

        AssertEvenementPresent(historique, TypeEvenement.DebutProcessus);
        AssertEvenementPresent(historique, TypeEvenement.EntreeNoeud, "saisir-informations");
        AssertEvenementPresent(historique, TypeEvenement.FinProcessus);
    }

    [Fact]
    public async Task SousProcessus_DefinitionEnfantUtiliseeParNombreInstances()
    {
        // Deux agrégats différents → deux instances parent distinctes, chacune avec son enfant
        var id1 = await DemarrerAsync(CleParent, aggregateId: 7008);
        var id2 = await DemarrerAsync(CleParent, aggregateId: 7009);

        var enfants1 = await ObtenirEnfantsAsync(id1);
        var enfants2 = await ObtenirEnfantsAsync(id2);

        Assert.Single(enfants1);
        Assert.Single(enfants2);
        Assert.NotEqual(enfants1[0].Id, enfants2[0].Id);
    }
}
