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
        var defCommande = new ProcessusBuilder(
                "commande-achat",
                "Approbation de commande d'achat",
                "valider-commande")
            .Metier("valider-commande", "Valider la commande", "approbation-responsable")
            .Interactif("approbation-responsable", "Approbation responsable", n => n
                .Tache("Approuver la commande",
                    "Vérifiez et approuvez ou refusez la commande d'achat.")
                .Vers("decision-approbation"))
            .Decision("decision-approbation", "Décision", n => n
                .SiEgal("approuve", true).Vers("notification-approbation")
                .Defaut().Vers("notification-refus"))
            .Metier("notification-approbation", "Notifier approbation")
            .Metier("notification-refus", "Notifier refus")
            .Build();

        await bpm.SauvegarderDefinitionAsync(defCommande);
        await bpm.PublierDefinitionAsync("commande-achat");

        // ── 2. Processus d'intégration employé (avec signal + sous-processus) ─
        var defOnboarding = new ProcessusBuilder(
                "onboarding-employe",
                "Intégration nouvel employé",
                "creer-compte")
            .Metier("creer-compte", "Créer compte employé", "configurer-acces")
            .Interactif("configurer-acces", "Configuration des accès", n => n
                .Tache("Configurer les droits d'accès",
                    "Définir les rôles et permissions du nouvel employé.")
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
        var defFormation = new ProcessusBuilder(
                "formation-securite",
                "Formation sécurité obligatoire",
                "module-incendie")
            .Interactif("module-incendie", "Module incendie", n => n
                .Tache("Compléter le module incendie")
                .Vers("module-informatique"))
            .Interactif("module-informatique", "Module informatique", n => n
                .Tache("Compléter le module informatique")
                .Vers("delai-quiz"))
            .AttenteTemps("delai-quiz", "Délai avant quiz", n => n
                .Echeance(DateTime.UtcNow.AddMinutes(5))
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
