import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTabsModule } from '@angular/material/tabs';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDividerModule } from '@angular/material/divider';
import { forkJoin, of, Observable } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { BpmService } from '../../../core/services/bpm.service';
import { MermaidService } from '../../../core/services/mermaid.service';
import { InstanceProcessus, EvenementInstance, ResultatMigration } from '../../../core/models/instance.model';
import { DefinitionProcessus } from '../../../core/models/definition.model';
import { MermaidDiagramComponent } from '../../../shared/components/mermaid-diagram/mermaid-diagram.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-instance-detail',
  imports: [
    CommonModule, RouterModule, ReactiveFormsModule,
    MatCardModule, MatIconModule, MatButtonModule, MatTabsModule,
    MatProgressSpinnerModule, MatSnackBarModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatTooltipModule, MatDividerModule,
    MermaidDiagramComponent, StatusBadgeComponent,
  ],
  templateUrl: './instance-detail.component.html',
  styleUrl: './instance-detail.component.scss',
})
export class InstanceDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly bpm = inject(BpmService);
  private readonly mermaidSvc = inject(MermaidService);
  private readonly snack = inject(MatSnackBar);

  id!: number;
  instance?: InstanceProcessus;
  definition?: DefinitionProcessus;
  historique: EvenementInstance[] = [];
  enfants: InstanceProcessus[] = [];
  variables: Record<string, unknown> = {};
  signaux: string[] = [];
  tache: { idTache?: number; logon?: string } = {};
  diagram = '';
  loading = true;
  actionLoading = false;

  signalCtrl = new FormControl('', Validators.required);
  logonCtrl  = new FormControl('', Validators.required);
  varNomCtrl = new FormControl('', Validators.required);
  varValCtrl = new FormControl('');
  editingVar: string | null = null;

  versionMigrationCtrl = new FormControl<number | null>(null, [Validators.required, Validators.min(1)]);
  migrationLoading = false;
  dernierResultatMigration: ResultatMigration | null = null;

  readonly objectKeys = Object.keys;

  ngOnInit(): void {
    this.id = Number(this.route.snapshot.paramMap.get('id'));
    this.charger();
  }

  charger(): void {
    this.loading = true;
    this.bpm.getInstance(this.id).subscribe({
      next: instance => { this.instance = instance; this.chargerDetails(); },
      error: () => { this.loading = false; },
    });
  }

  private chargerDetails(): void {
    if (!this.instance) return;
    const inst = this.instance;

    forkJoin({
      historique: this.bpm.getHistorique(this.id),
      variables:  this.bpm.getVariables(this.id),
      enfants:    this.bpm.getEnfants(this.id),
      definition: this.bpm.getDefinitionVersion(inst.cleDefinition, inst.versionDefinition)
                      .pipe(catchError(() => of(undefined as DefinitionProcessus | undefined))),
      signaux:    inst.statut === 'Suspendue'
                    ? this.bpm.getSignaux(this.id).pipe(catchError(() => of([] as string[])))
                    : of([] as string[]),
      tache:      inst.statut === 'Suspendue'
                    ? this.bpm.getTache(this.id).pipe(catchError(() => of({})))
                    : of({} as { idTache?: number; logon?: string }),
    }).subscribe(({ historique, variables, enfants, definition, signaux, tache }) => {
      this.historique = historique;
      this.variables  = variables;
      this.enfants    = enfants;
      this.signaux    = signaux;
      this.tache      = tache;
      if (definition) {
        this.definition = definition;
        this.diagram = this.mermaidSvc.generateDiagram(definition, inst.idNoeudCourant);
      }
      this.loading = false;
    });
  }

  // ── Actions ─────────────────────────────────────────────────────────────────

  terminerEtape(): void {
    this.exec(this.bpm.terminerEtape(this.id), 'Étape terminée avec succès.');
  }

  reprendreTimer(): void {
    this.exec(this.bpm.reprendreTimer(this.id), 'Minuterie reprise.');
  }

  envoyerSignal(): void {
    if (!this.signalCtrl.valid) return;
    const nom = this.signalCtrl.value!;
    this.exec(this.bpm.envoyerSignal(this.id, nom), `Signal "${nom}" envoyé.`);
    this.signalCtrl.reset();
  }

  assigner(): void {
    if (!this.logonCtrl.valid) return;
    const logon = this.logonCtrl.value!;
    this.exec(this.bpm.assigner(this.id, logon), `Tâche assignée à "${logon}".`);
  }

  enregistrerVariable(): void {
    if (!this.varNomCtrl.valid) return;
    const nom    = this.varNomCtrl.value!;
    const valeur = this.varValCtrl.value;
    this.exec(this.bpm.modifierVariable(this.id, nom, valeur), `Variable "${nom}" mise à jour.`);
    this.editingVar = null;
    this.varNomCtrl.reset(); this.varValCtrl.reset();
  }

  ouvrirEditionVar(nom: string, valeur: unknown): void {
    this.editingVar = nom;
    this.varNomCtrl.setValue(nom);
    this.varValCtrl.setValue(String(valeur ?? ''));
  }

  private exec(obs: Observable<void>, successMsg: string): void {
    this.actionLoading = true;
    obs.subscribe({
      next: () => {
        this.snack.open(successMsg, 'OK', { duration: 3000 });
        this.charger();
        this.actionLoading = false;
      },
      error: (err: any) => {
        const msg = err?.error?.erreur ?? 'Une erreur est survenue.';
        this.snack.open(msg, 'OK', { duration: 5000 });
        this.actionLoading = false;
      },
    });
  }

  // ── Helpers ─────────────────────────────────────────────────────────────────

  get isSuspendue(): boolean { return this.instance?.statut === 'Suspendue'; }

  get noeudCourantType(): string {
    if (!this.definition || !this.instance?.idNoeudCourant) return '';
    return this.definition.noeuds.find(n => n.id === this.instance!.idNoeudCourant)?.type ?? '';
  }

  get canTerminerEtape():  boolean { return this.isSuspendue && this.noeudCourantType === 'NoeudInteractif'; }
  get canReprendreTimer(): boolean { return this.isSuspendue && this.noeudCourantType === 'NoeudAttenteTemps'; }
  get canSignal():         boolean { return this.isSuspendue && this.noeudCourantType === 'NoeudAttenteSignal'; }
  get canMigrer():         boolean {
    const s = this.instance?.statut;
    return s === 'Active' || s === 'Suspendue';
  }

  migrer(): void {
    if (!this.versionMigrationCtrl.valid || this.versionMigrationCtrl.value === null) return;
    const version = this.versionMigrationCtrl.value;
    this.migrationLoading = true;
    this.dernierResultatMigration = null;
    this.bpm.migrerInstance(this.id, version).subscribe({
      next: resultat => {
        this.dernierResultatMigration = resultat;
        if (resultat.succes) {
          this.snack.open(`Migration vers v${version} réussie.`, 'OK', { duration: 4000 });
          this.charger();
        } else {
          this.snack.open(resultat.messageErreur ?? 'Migration échouée.', 'OK', { duration: 6000 });
        }
        this.migrationLoading = false;
      },
      error: (err: any) => {
        const msg = err?.error?.erreur ?? 'Erreur lors de la migration.';
        this.snack.open(msg, 'OK', { duration: 6000 });
        this.migrationLoading = false;
      },
    });
  }

  typeEvenementLabel(type: string): string {
    const map: Record<string, string> = {
      DebutProcessus: 'Début processus', EntreeNoeud: 'Entrée nœud',
      SortieNoeud: 'Sortie nœud', NoeudSuspendu: 'Suspendu', NoeudRepris: 'Repris',
      ErreurNoeud: 'Erreur', FinProcessus: 'Fin processus', MigrationInstance: 'Migration',
      SignalRecu: 'Signal reçu', VariableModifiee: 'Variable modifiée', TacheAssignee: 'Tâche assignée',
    };
    return map[type] ?? type;
  }

  typeEvenementIcon(type: string): string {
    const map: Record<string, string> = {
      DebutProcessus: 'play_arrow', EntreeNoeud: 'login', SortieNoeud: 'logout',
      NoeudSuspendu: 'pause', NoeudRepris: 'play_arrow', ErreurNoeud: 'error',
      FinProcessus: 'stop', MigrationInstance: 'sync_alt', SignalRecu: 'notifications',
      VariableModifiee: 'edit', TacheAssignee: 'person',
    };
    return map[type] ?? 'circle';
  }

  resultatClass(resultat: string): string {
    if (resultat === 'Erreur') return 'ev-erreur';
    if (resultat === 'Suspendu') return 'ev-suspendu';
    return 'ev-succes';
  }

  formatDuree(ms?: number): string {
    if (!ms) return '';
    return ms < 1000 ? `${ms} ms` : `${(ms / 1000).toFixed(1)} s`;
  }
}
