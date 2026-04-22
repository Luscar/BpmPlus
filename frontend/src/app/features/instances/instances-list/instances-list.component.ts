import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BpmService } from '../../../core/services/bpm.service';
import { InstanceProcessus, StatutInstance } from '../../../core/models/instance.model';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-instances-list',
  standalone: true,
  imports: [
    CommonModule, RouterModule, ReactiveFormsModule,
    MatTableModule, MatCardModule, MatIconModule, MatButtonModule,
    MatProgressSpinnerModule, MatSelectModule, MatFormFieldModule,
    MatInputModule, MatTooltipModule, StatusBadgeComponent,
  ],
  templateUrl: './instances-list.component.html',
  styleUrl: './instances-list.component.scss',
})
export class InstancesListComponent implements OnInit {
  private readonly bpm = inject(BpmService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  instances: InstanceProcessus[] = [];
  loading = true;

  statutCtrl = new FormControl<StatutInstance | ''>('');
  definitionCtrl = new FormControl('');

  statuts: Array<{ value: StatutInstance | ''; label: string }> = [
    { value: '', label: 'Tous les statuts' },
    { value: 'Active', label: 'Active' },
    { value: 'Suspendue', label: 'Suspendue' },
    { value: 'EnErreur', label: 'En erreur' },
    { value: 'Terminee', label: 'Terminée' },
  ];

  displayedColumns = ['id', 'definition', 'statut', 'noeud', 'parent', 'dateDebut', 'dateMaj', 'actions'];

  ngOnInit(): void {
    const qs = this.route.snapshot.queryParams;
    if (qs['statut']) this.statutCtrl.setValue(qs['statut']);
    if (qs['cleDefinition']) this.definitionCtrl.setValue(qs['cleDefinition']);
    this.charger();

    this.statutCtrl.valueChanges.subscribe(() => this.charger());
    this.definitionCtrl.valueChanges.subscribe(() => this.charger());
  }

  charger(): void {
    this.loading = true;
    const statut = this.statutCtrl.value as StatutInstance | undefined;
    const def    = this.definitionCtrl.value || undefined;
    this.bpm.getInstances(statut || undefined, def).subscribe({
      next: instances => { this.instances = instances; this.loading = false; },
      error: () => this.loading = false,
    });
  }

  naviguerInstance(id: number): void {
    this.router.navigate(['/instances', id]);
  }
}
