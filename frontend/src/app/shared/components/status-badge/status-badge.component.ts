import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { StatutInstance } from '../../../core/models/instance.model';
import { StatutDefinition } from '../../../core/models/definition.model';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  template: `
    <span class="badge" [class]="badgeClass">
      <mat-icon class="badge-icon">{{ icon }}</mat-icon>
      {{ label }}
    </span>
  `,
  styles: [`
    .badge {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 3px 10px;
      border-radius: 12px;
      font-size: 12px;
      font-weight: 500;
      white-space: nowrap;
    }
    .badge-icon { font-size: 14px; width: 14px; height: 14px; }
    .badge-active    { background: #e8f5e9; color: #2e7d32; }
    .badge-suspendue { background: #fff3e0; color: #e65100; }
    .badge-erreur    { background: #ffebee; color: #c62828; }
    .badge-terminee  { background: #eeeeee; color: #424242; }
    .badge-publiee   { background: #e3f2fd; color: #1565c0; }
    .badge-brouillon { background: #fafafa; color: #757575; border: 1px solid #e0e0e0; }
  `],
})
export class StatusBadgeComponent {
  @Input({ required: true }) statut!: StatutInstance | StatutDefinition;

  get badgeClass(): string {
    const map: Record<string, string> = {
      Active:    'badge-active',
      Suspendue: 'badge-suspendue',
      EnErreur:  'badge-erreur',
      Terminee:  'badge-terminee',
      Publiee:   'badge-publiee',
      Brouillon: 'badge-brouillon',
    };
    return `badge ${map[this.statut] ?? ''}`;
  }

  get icon(): string {
    const map: Record<string, string> = {
      Active:    'play_circle',
      Suspendue: 'pause_circle',
      EnErreur:  'error',
      Terminee:  'check_circle',
      Publiee:   'verified',
      Brouillon: 'edit_note',
    };
    return map[this.statut] ?? 'circle';
  }

  get label(): string {
    const map: Record<string, string> = {
      Active:    'Active',
      Suspendue: 'Suspendue',
      EnErreur:  'En erreur',
      Terminee:  'Terminée',
      Publiee:   'Publiée',
      Brouillon: 'Brouillon',
    };
    return map[this.statut] ?? this.statut;
  }
}
