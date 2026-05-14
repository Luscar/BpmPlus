using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;

namespace BpmPlus.ExempleClient;

// ─────────────────────────────────────────────────────────────────────────────
//  ExempleDefinitionBuilder
//
//  Shows the same "approbation-commande" process written with the V2 builder,
//  then demonstrates a more complex "gestion-achat" process that shows off:
//
//    • Phase grouping            — organises nodes by logical stage
//    • Fluent condition DSL      — SiVariable("x").EstEgalA("y").Aller("n")
//    • Rich task sub-builder     — TacheHumaine(t => t.Titre(…).Role(…))
//    • PatternApprobation helper — wires an interactive + decision in one call
//    • EcheanceQuery on waits    — deadline resolved by a runtime query
//    • BuildStrict()             — validates dead-ends and orphan nodes
//    • Build(out mermaid)        — generates a Mermaid flowchart
//    • .ToMermaid() extension    — works on any existing DefinitionProcessus
// ─────────────────────────────────────────────────────────────────────────────

public static class ExempleDefinitionBuilder
{
    // ── Reproduire l'exemple existant en style V2 ──────────────────────────────

    /// <summary>
    /// The same order-approval process from Program.cs, rewritten with the v2
    /// builder to illustrate how phase grouping and the richer DSL compare with
    /// the original.
    /// </summary>
    public static DefinitionProcessus ApprobationCommandeV2()
    {
        return DefinitionBuilder
            .Definir("approbation-commande-v2")
            .Intitule("Processus d'approbation de commande (V2)")
            .Description(
                "Cycle de validation d'une commande : contrôle initial, " +
                "approbation humaine et notification du résultat.")
            .Auteur("Équipe BpmPlus")
            .Etiquettes("commandes", "approbation", "finance")
            .Commence("valider-commande")

            // ── Phase 1 ────────────────────────────────────────────────────────
            .Phase("Validation initiale", phase => phase
                .Metier("valider-commande", "Valider la commande", "approbation-responsable"))

            // ── Phase 2 : approval pattern (interactive + decision in one call) ──
            .PatternApprobation(
                idTache:          "approbation-responsable",
                intituleTache:    "Approbation responsable",
                idDecision:       "decision-approbation",
                commandeDecision: "EnregistrerDecisionCommand",
                queryDecision:    "EstCommandeApprouveeQuery",
                versApprouve:     "notification-approbation",
                versRefuse:       "notification-refus")

            // ── Phase 3 ────────────────────────────────────────────────────────
            .Phase("Notifications", phase => phase
                .Metier("notification-approbation", "Notifier approbation")
                .Metier("notification-refus",       "Notifier refus"))

            .Build();
    }

    // ── Processus plus complexe démontrant toutes les fonctionnalités V2 ───────

    /// <summary>
    /// A multi-phase purchase-request process. Intentionally verbose so every
    /// v2 feature appears at least once in context.
    /// </summary>
    public static DefinitionProcessus GestionAchat(out string mermaid)
    {
        return DefinitionBuilder
            .Definir("gestion-achat")
            .Intitule("Gestion des demandes d'achat")
            .Description(
                "Du bon de commande à la livraison : validation budgétaire, " +
                "approbation hiérarchique, suivi livraison et clôture.")
            .Auteur("Équipe Achats")
            .Etiquettes("achat", "finance", "approbation", "logistique")
            .Commence("verifier-budget")

            // ═══ Phase 1 : Vérification budgétaire ════════════════════════════
            .Phase("Vérification budgétaire", phase => phase

                .Metier("verifier-budget", "Vérifier le budget disponible", b => b
                    .Commande("VerifierBudgetCommand")
                    .Param("montant")
                    .Param("centreCoût")
                    .Puis("decision-budget"))

                .Decision("decision-budget", "Budget suffisant ?", d => d
                    // Fluent variable conditions — easy to read at a glance
                    .SiVariable("budgetDisponible").EstEgalA(false).Aller("refus-budget")
                    .SiVariable("montant").EstSuperieurA(50_000m).Aller("approbation-directeur")
                    .Sinon.Aller("approbation-responsable"))

                .Metier("refus-budget", "Refus : budget insuffisant", b => b
                    .Commande("NotifierRefusBudgetCommand")
                    .Param("centreCoût")
                    .Final()))

            // ═══ Phase 2 : Approbation responsable ════════════════════════════
            .Phase("Approbation hiérarchique", phase => phase

                // Rich task sub-builder — all task properties in one discoverable block
                .Interactif("approbation-responsable", "Approbation du responsable", n => n
                    .TacheHumaine(t => t
                        .Titre("Valider la demande d'achat")
                        .Description(
                            "Vérifiez les justificatifs et les devis avant de valider.")
                        .Role("RESPONSABLE_ACHAT"))
                    .AuRetour("EnregistrerDecisionResponsableCommand")
                    .Puis("decision-responsable"))

                .Decision("decision-responsable", "Décision responsable", d => d
                    .SiVariable("décision").EstEgalA("Approuvé").Aller("creation-commande")
                    .Sinon.Aller("refus-responsable"))

                .Metier("refus-responsable", "Refus par le responsable", b => b
                    .Commande("NotifierRefusResponsableCommand")
                    .Param("demandeurLogon")
                    .Final())

                // High-value orders also require a director sign-off
                .Interactif("approbation-directeur", "Approbation du directeur", n => n
                    .TacheHumaine(t => t
                        .Titre("Valider la demande d'achat (montant élevé)")
                        .Description(
                            "Cette demande dépasse 50 000 € et nécessite votre approbation.")
                        .Role("DIRECTEUR")
                        .EstUneRevision())
                    .AuRetour("EnregistrerDecisionDirecteurCommand")
                    .Puis("decision-directeur"))

                .Decision("decision-directeur", "Décision directeur", d => d
                    .SiVariable("décision").EstEgalA("Approuvé").Aller("creation-commande")
                    .Sinon.Aller("refus-responsable")))

            // ═══ Phase 3 : Création de la commande et suivi livraison ═════════
            .Phase("Commande & livraison", phase => phase

                .Metier("creation-commande", "Créer la commande fournisseur", b => b
                    .Commande("CreerCommandeFournisseurCommand")
                    .Param("fournisseurId")
                    .Param("montant")
                    .Puis("attente-livraison"))

                // Deadline computed at runtime by a query — avoids baking in a static date
                .AttenteTemps("attente-livraison", "Attente de la livraison", t => t
                    .EcheanceQuery("DateLivraisonPrevueQuery", q => q
                        .Param("commandeId"))
                    .Puis("reception-livraison"))

                // Signal-based resume when goods arrive at the warehouse
                .AttenteSignal("reception-livraison", "RéceptionMarchandisesSignal",
                    vers: "confirmer-reception")

                .Interactif("confirmer-reception", "Confirmation de réception", n => n
                    .TacheHumaine(t => t
                        .Titre("Confirmer la réception des marchandises")
                        .Role("MAGASINIER")
                        .Description("Vérifiez la conformité de la livraison avant de valider."))
                    .AuRetour("ConfirmerReceptionCommand")
                    .Puis("decision-reception"))

                .Decision("decision-reception", "Livraison conforme ?", d => d
                    .SiVariable("livraisonConforme").EstEgalA(true).Aller("cloturer-achat")
                    .Sinon.Aller("litige-livraison"))

                .Interactif("litige-livraison", "Gestion d'un litige livraison", n => n
                    .TacheHumaine(t => t
                        .Titre("Traiter le litige de livraison")
                        .Role("RESPONSABLE_ACHAT")
                        .Description("La livraison n'est pas conforme. Contactez le fournisseur."))
                    .AuRetour("EnregistrerResolutionLitigeCommand")
                    .Puis("cloturer-achat")))

            // ═══ Phase 4 : Clôture ════════════════════════════════════════════
            .Phase("Clôture", phase => phase

                .Metier("cloturer-achat", "Clôturer la demande d'achat", b => b
                    .Commande("CloturerAchatCommand")
                    .Param("commandeId")
                    .Final()))

            // Build + emit Mermaid diagram in one call
            .Build(out mermaid);
    }

    // ── Affichage ──────────────────────────────────────────────────────────────

    public static void AfficherDemo()
    {
        Console.WriteLine();
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │  DefinitionBuilder — démonstration                              │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        // V2 version of the existing process
        var defV2 = ApprobationCommandeV2();
        Console.WriteLine($"  ✓ '{defV2.Cle}' — {defV2.Noeuds.Count} nœuds, " +
                          $"nœud de début : '{defV2.NoeudDebutId}'");

        // Complex purchase process with Mermaid export
        var defAchat = GestionAchat(out var mermaid);
        Console.WriteLine($"  ✓ '{defAchat.Cle}' — {defAchat.Noeuds.Count} nœuds");
        Console.WriteLine();

        // ToMermaid() extension — works on any DefinitionProcessus (v1 or v2)
        var mermaidV2 = defV2.ToMermaid();
        Console.WriteLine("  Diagramme Mermaid (approbation-commande-v2) :");
        Console.WriteLine("  " + new string('─', 60));
        foreach (var line in mermaidV2.Split('\n'))
            Console.WriteLine("  " + line);

        Console.WriteLine();
        Console.WriteLine("  Diagramme Mermaid (gestion-achat — extrait des 10 premières lignes) :");
        Console.WriteLine("  " + new string('─', 60));
        foreach (var line in mermaid.Split('\n').Take(10))
            Console.WriteLine("  " + line);
        Console.WriteLine("  …");

        Console.WriteLine();
        Console.WriteLine("  Collez le diagramme sur https://mermaid.live pour le visualiser.");
        Console.WriteLine();
    }
}
