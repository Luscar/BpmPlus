import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTabsModule } from '@angular/material/tabs';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { BpmService } from '../../../core/services/bpm.service';
import { MermaidService } from '../../../core/services/mermaid.service';
import { DefinitionProcessus } from '../../../core/models/definition.model';
import { MermaidDiagramComponent } from '../../../shared/components/mermaid-diagram/mermaid-diagram.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-definition-detail',
  standalone: true,
  imports: [
    CommonModule, RouterModule,
    MatCardModule, MatIconModule, MatButtonModule, MatTabsModule,
    MatChipsModule, MatProgressSpinnerModule, MatSnackBarModule,
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
