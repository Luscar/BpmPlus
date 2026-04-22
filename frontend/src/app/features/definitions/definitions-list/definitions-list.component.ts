import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { BpmService } from '../../../core/services/bpm.service';
import { DefinitionResume } from '../../../core/models/definition.model';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-definitions-list',
  standalone: true,
  imports: [
    CommonModule, RouterModule,
    MatTableModule, MatCardModule, MatIconModule,
    MatButtonModule, MatProgressSpinnerModule, MatChipsModule,
    StatusBadgeComponent,
  ],
  templateUrl: './definitions-list.component.html',
  styleUrl: './definitions-list.component.scss',
})
export class DefinitionsListComponent implements OnInit {
  private readonly bpm = inject(BpmService);

  definitions: DefinitionResume[] = [];
  loading = true;
  displayedColumns = ['nom', 'cle', 'version', 'statut', 'noeuds', 'dateCreation', 'actions'];

  ngOnInit(): void {
    this.bpm.getDefinitions().subscribe({
      next: d => { this.definitions = d; this.loading = false; },
      error: () => this.loading = false,
    });
  }
}
