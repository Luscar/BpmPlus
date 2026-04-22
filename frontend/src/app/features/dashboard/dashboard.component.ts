import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { BpmService } from '../../core/services/bpm.service';
import { DashboardStats } from '../../core/models/instance.model';

@Component({
  selector: 'app-dashboard',
  imports: [
    CommonModule, RouterModule,
    MatCardModule, MatIconModule, MatButtonModule, MatProgressSpinnerModule,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  private readonly bpm = inject(BpmService);

  stats?: DashboardStats;
  loading = true;
  error?: string;

  ngOnInit(): void {
    this.bpm.getDashboardStats().subscribe({
      next: s => { this.stats = s; this.loading = false; },
      error: () => { this.error = 'Impossible de charger les statistiques.'; this.loading = false; },
    });
  }
}
