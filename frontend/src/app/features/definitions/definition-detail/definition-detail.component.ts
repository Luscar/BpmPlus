import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTabsModule } from '@angular/material/tabs';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BpmService } from '../../../core/services/bpm.service';
import { MermaidService } from '../../../core/services/mermaid.service';
import { DefinitionProcessus } from '../../../core/models/definition.model';
import { ResultatMigration } from '../../../core/models/instance.model';
import { MermaidDiagramComponent } from '../../../shared/components/mermaid-diagram/mermaid-diagram.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-definition-detail',
  imports: [
    CommonModule, RouterModule, ReactiveFormsModule,
    MatCardModule, MatIconModule, MatButtonModule, MatTabsModule,
    MatChipsModule, MatProgressSpinnerModule, MatSnackBarModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, MatTableModule, MatTooltipModule,
    MermaidDiagramComponent, StatusBadgeComponent,
  ],
  templateUrl: './definition-detail.component.html',
  styleUrl: './definition-detail.component.scss',
})
export class DefinitionDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly bpm = inject(BpmService);
  private readonly mermaidSvc = inject(MermaidService);
  private readonly snack = inject(MatSnackBar);

  cle!: string;
  versions: DefinitionProcessus[] = [];
  selected?: DefinitionProcessus;
  diagram = '';
  loading = true;
  publishing = false;

  versionMigrationCtrl = new FormControl<number | null>(null, [Validators.required, Validators.min(1)]);
  migrationLoading = false;
  resultats: ResultatMigration[] = [];
  readonly migrationColumns = ['idInstance', 'ancienneVersion', 'nouvelleVersion', 'ancienNoeudId', 'nouveauNoeudId', 'statut'];

  ngOnInit(): void {
    this.cle = this.route.snapshot.paramMap.get('cle')!;
    this.bpm.getDefinitionVersions(this.cle).subscribe({
      next: versions => {
        this.versions = versions;
        this.selectVersion(versions[0]);
        this.loading = false;
      },
      error: () => this.loading = false,
    });
  }

  selectVersion(def: DefinitionProcessus): void {
    this.selected = def;
    this.diagram = this.mermaidSvc.generateDiagram(def);
  }

  publier(): void {
    if (!this.selected) return;
    this.publishing = true;
    this.bpm.publierDefinition(this.cle).subscribe({
      next: () => {
        this.snack.open('Définition publiée avec succès.', 'OK', { duration: 3000 });
        this.ngOnInit();
      },
      error: () => {
        this.snack.open('Erreur lors de la publication.', 'OK', { duration: 3000 });
        this.publishing = false;
      },
    });
  }

  get nbSucces(): number { return this.resultats.filter(r => r.succes).length; }
  get nbEchecs(): number { return this.resultats.filter(r => !r.succes).length; }

  nodeTypeLabel(type: string): string {
    const map: Record<string, string> = {
      NoeudMetier: 'Métier',
      NoeudInteractif: 'Tâche humaine',
      NoeudDecision: 'Décision',
      NoeudAttenteTemps: 'Attente temps',
      NoeudAttenteSignal: 'Attente signal',
      NoeudSousProcessus: 'Sous-processus',
    };
    return map[type] ?? type;
  }

  migrerInstances(): void {
    if (!this.versionMigrationCtrl.valid || this.versionMigrationCtrl.value === null) return;
    const version = this.versionMigrationCtrl.value;
    this.migrationLoading = true;
    this.resultats = [];
    this.bpm.migrerToutesInstances(this.cle, version).subscribe({
      next: resultats => {
        this.resultats = resultats;
        const succes = resultats.filter(r => r.succes).length;
        const echecs = resultats.length - succes;
        const msg = resultats.length === 0
          ? 'Aucune instance éligible à migrer.'
          : `Migration terminée : ${succes} réussie(s), ${echecs} échouée(s).`;
        this.snack.open(msg, 'OK', { duration: 5000 });
        this.migrationLoading = false;
      },
      error: (err: any) => {
        const msg = err?.error?.erreur ?? 'Erreur lors de la migration en masse.';
        this.snack.open(msg, 'OK', { duration: 6000 });
        this.migrationLoading = false;
      },
    });
  }

  nodeTypeIcon(type: string): string {
    const map: Record<string, string> = {
      NoeudMetier: 'settings',
      NoeudInteractif: 'person',
      NoeudDecision: 'call_split',
      NoeudAttenteTemps: 'timer',
      NoeudAttenteSignal: 'notifications',
      NoeudSousProcessus: 'account_tree',
    };
    return map[type] ?? 'circle';
  }
}
