using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;

namespace BpmPlus.Api.Infrastructure;

public static class SeedData
{
    public static async Task InitialiserAsync(IServiceBpm bpm)
    {
        var existantes = await bpm.ObtenirDefinitionsAsync();
        if (existantes.Any()) return;

        // ── 1. Processus d'approbation de commande ───────────────────────────
        var defCommande = DefinitionBuilder.Definir("commande-achat")
            .Intitule("Approbation de commande d'achat")
            .Commence("valider-commande")
            .Metier("valider-commande", "Valider la commande", "approbation-responsable")
            .Interactif("approbation-responsable", "Approbation responsable", n => n
                .Titre("Approuver la commande")
                .Description("Vérifiez et approuvez ou refusez la commande d'achat.")
                .Vers("decision-approbation"))
            .Decision("decision-approbation", "Décision", n => n
                .SiVariable("approuve").EstEgalA(true).Aller("notification-approbation")
                .Sinon.Aller("notification-refus"))
            .Metier("notification-approbation", "Notifier approbation")
            .Metier("notification-refus", "Notifier refus")
            .Build();

        await bpm.SauvegarderDefinitionAsync(defCommande);
        await bpm.PublierDefinitionAsync("commande-achat");

        // ── 2. Processus d'intégration employé (avec signal + sous-processus) ─
        var defOnboarding = DefinitionBuilder.Definir("onboarding-employe")
            .Intitule("Intégration nouvel employé")
            .Commence("creer-compte")
            .Metier("creer-compte", "Créer compte employé", "configurer-acces")
            .Interactif("configurer-acces", "Configuration des accès", n => n
                .Titre("Configurer les droits d'accès")
                .Description("Définir les rôles et permissions du nouvel employé.")
                .Vers("attendre-badge"))
            .AttenteSignal("attendre-badge", "Attente badge physique", "badge-livre", "formation-obligatoire")
            .SousProcessus("formation-obligatoire", "Formation obligatoire", n => n
                .Definition("formation-securite", 1)
                .Vers("notification-fin"))
            .Metier("notification-fin", "Notifier fin onboarding")
            .Build();

        await bpm.SauvegarderDefinitionAsync(defOnboarding);
        await bpm.PublierDefinitionAsync("onboarding-employe");

        // ── 3. Processus de formation sécurité (sous-processus) ──────────────
        var defFormation = DefinitionBuilder.Definir("formation-securite")
            .Intitule("Formation sécurité obligatoire")
            .Commence("module-incendie")
            .Interactif("module-incendie", "Module incendie", n => n
                .Titre("Compléter le module incendie")
                .Vers("module-informatique"))
            .Interactif("module-informatique", "Module informatique", n => n
                .Titre("Compléter le module informatique")
                .Vers("delai-quiz"))
            .AttenteTemps("delai-quiz", "Délai avant quiz", n => n
                .EcheanceFixe(DateTime.UtcNow.AddMinutes(5))
                .Vers("quiz-final"))
            .Metier("quiz-final", "Passer quiz final")
            .Build();

        await bpm.SauvegarderDefinitionAsync(defFormation);
        await bpm.PublierDefinitionAsync("formation-securite");

        // ── Instances de démonstration ────────────────────────────────────────
        await bpm.DemarrerAsync("commande-achat", 1001,
            new Dictionary<string, object?> { ["montant"] = 4500m, ["fournisseur"] = "Acme Corp" });

        await bpm.DemarrerAsync("commande-achat", 1002,
            new Dictionary<string, object?> { ["montant"] = 890m, ["fournisseur"] = "TechSupply" });

        await bpm.DemarrerAsync("onboarding-employe", 2001,
            new Dictionary<string, object?> { ["prenom"] = "Alice", ["nom"] = "Martin" });
    }
}
