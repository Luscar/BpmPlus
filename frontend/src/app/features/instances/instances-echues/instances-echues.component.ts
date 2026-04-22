import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BpmService } from '../../../core/services/bpm.service';
import { InstanceEchue } from '../../../core/models/instance.model';

@Component({
  selector: 'app-instances-echues',
  imports: [
    CommonModule, RouterModule,
    MatCardModule, MatIconModule, MatButtonModule,
    MatProgressSpinnerModule, MatSnackBarModule, MatTooltipModule,
  ],
  template: `
    <div class="page">
      <h1 class="page-title">
        <mat-icon>timer_off</mat-icon>
        Instances avec minuterie échue
      </h1>
      <p class="subtitle">
        Ces instances sont en attente d'un nœud temps dont l'échéance est dépassée.
        Utilisez "Reprendre" pour les relancer.
      </p>

      @if (loading) {
        <div class="center"><mat-spinner /></div>
      } @else {
        <div class="echues-grid">
          @for (e of echues; track e.idInstance) {
            <mat-card class="echue-card">
              <mat-card-content>
                <div class="echue-header">
                  <mat-icon class="timer-icon">timer_off</mat-icon>
                  <div>
                    <div class="echue-id">Instance <strong>#{{ e.idInstance }}</strong></div>
                    <div class="echue-date">Échue le {{ e.dateEcheance | date:'dd/MM/yyyy HH:mm' }}</div>
                  </div>
                </div>
              </mat-card-content>
              <mat-card-actions>
                <button mat-button [routerLink]="['/instances', e.idInstance]">
                  <mat-icon>open_in_new</mat-icon> Voir
                </button>
                <button mat-raised-button color="warn"
                  (click)="reprendre(e.idInstance)" [disabled]="reprendreEnCours[e.idInstance]">
                  <mat-icon>fast_forward</mat-icon> Reprendre
                </button>
              </mat-card-actions>
            </mat-card>
          }
          @if (echues.length === 0) {
            <div class="empty">
              <mat-icon>check_circle</mat-icon>
              <p>Aucune instance échue. Tout est à jour !</p>
            </div>
          }
        </div>
        <button mat-icon-button class="refresh-btn" (click)="charger()" matTooltip="Rafraîchir">
          <mat-icon>refresh</mat-icon>
        </button>
      }
    </div>
  `,
  styles: [`
    .page { padding: 24px; max-width: 1000px; }
    .page-title { display: flex; align-items: center; gap: 12px; font-size: 28px; font-weight: 300; margin-bottom: 8px; color: #4a148c; mat-icon { font-size: 32px; width: 32px; height: 32px; } }
    .subtitle { color: #546e7a; font-size: 14px; margin-bottom: 24px; }
    .echues-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 16px; }
    .echue-card { border-left: 4px solid #9c27b0; }
    .echue-header { display: flex; align-items: center; gap: 16px; }
    .timer-icon { font-size: 36px; width: 36px; height: 36px; color: #9c27b0; }
    .echue-id { font-size: 16px; }
    .echue-date { font-size: 13px; color: #757575; margin-top: 4px; }
    .empty { display: flex; flex-direction: column; align-items: center; padding: 48px; color: #9e9e9e; mat-icon { font-size: 48px; width: 48px; height: 48px; color: #4caf50; } }
    .center { display: flex; justify-content: center; padding: 48px; }
    .refresh-btn { margin-top: 16px; }
  `],
})
export class InstancesEchuesComponent implements OnInit {
  private readonly bpm = inject(BpmService);
  private readonly snack = inject(MatSnackBar);

  echues: InstanceEchue[] = [];
  loading = true;
  reprendreEnCours: Record<number, boolean> = {};

  ngOnInit(): void { this.charger(); }

  charger(): void {
    this.loading = true;
    this.bpm.getInstancesEchues().subscribe({
      next: e => { this.echues = e; this.loading = false; },
      error: () => this.loading = false,
    });
  }

  reprendre(id: number): void {
    this.reprendreEnCours[id] = true;
    this.bpm.reprendreTimer(id).subscribe({
      next: () => {
        this.snack.open(`Instance #${id} reprise.`, 'OK', { duration: 3000 });
        this.charger();
      },
      error: (err: any) => {
        const msg = err?.error?.erreur ?? 'Erreur lors de la reprise.';
        this.snack.open(msg, 'OK', { duration: 5000 });
        this.reprendreEnCours[id] = false;
      },
    });
  }
}
