import {
  Component,
  OnDestroy,
  OnInit,
  ViewChild,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipsModule } from '@angular/material/chips';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { BpmService } from '../../../core/services/bpm.service';
import {
  FiltreVariable,
  InstanceProcessus,
  OperateurVariable,
  RechercheInstancesQuery,
  ResultatRechercheInstances,
  StatutInstance,
} from '../../../core/models/instance.model';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';

const STATUTS_LABELS: Record<StatutInstance, string> = {
  Active: 'Active', Suspendue: 'Suspendue', EnErreur: 'En erreur', Terminee: 'Terminée',
};

type FiltreActifItem = { label: string; retirer: () => void };

const OPERATEUR_LABELS: Record<OperateurVariable, string> = {
  Egal:            '=',
  DifferentDe:     '≠',
  Contient:        'contient',
  CommencePar:     'commence par',
  TerminePar:      'se termine par',
  SuperieurA:      '>',
  SuperieurOuEgal: '≥',
  InferieurA:      '<',
  InferieurOuEgal: '≤',
};

const TAILLE_PAR_DEFAUT = 25;
const DEBOUNCE_MS = 400;

@Component({
  selector: 'app-instances-list',
  imports: [
    CommonModule, RouterModule, ReactiveFormsModule,
    MatTableModule, MatCardModule, MatIconModule, MatButtonModule,
    MatProgressSpinnerModule, MatProgressBarModule,
    MatSelectModule, MatFormFieldModule, MatInputModule,
    MatDatepickerModule, MatNativeDateModule,
    MatPaginatorModule, MatSortModule,
    MatTooltipModule, MatExpansionModule,
    MatCheckboxModule, MatChipsModule,
    StatusBadgeComponent,
  ],
  templateUrl: './instances-list.component.html',
  styleUrl: './instances-list.component.scss',
})
export class InstancesListComponent implements OnInit, OnDestroy {
  private readonly bpm     = inject(BpmService);
  private readonly route   = inject(ActivatedRoute);
  private readonly router  = inject(Router);
  private readonly fb      = inject(FormBuilder);
  private readonly destroy$ = new Subject<void>();

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  // ── État ────────────────────────────────────────────────────────────────────

  resultat?: ResultatRechercheInstances;
  loading = false;

  page   = 1;
  taille = TAILLE_PAR_DEFAUT;
  triColonne: string | undefined;
  triSens: 'asc' | 'desc' = 'desc';

  readonly toutesStatuts = Object.keys(STATUTS_LABELS) as StatutInstance[];
  readonly taillesPage   = [10, 25, 50, 100, 200];

  readonly operateurs: { valeur: OperateurVariable; label: string }[] = [
    { valeur: 'Egal',            label: '= égal à' },
    { valeur: 'DifferentDe',     label: '≠ différent de' },
    { valeur: 'Contient',        label: 'contient' },
    { valeur: 'CommencePar',     label: 'commence par' },
    { valeur: 'TerminePar',      label: 'se termine par' },
    { valeur: 'SuperieurA',      label: '> supérieur à' },
    { valeur: 'SuperieurOuEgal', label: '≥ supérieur ou égal' },
    { valeur: 'InferieurA',      label: '< inférieur à' },
    { valeur: 'InferieurOuEgal', label: '≤ inférieur ou égal' },
  ];

  readonly displayedColumns = [
    'id', 'aggregateId', 'definition', 'statut', 'noeud', 'parent',
    'dateDebut', 'dateMaj', 'actions',
  ];

  get nbFiltresActifs(): number {
    return this.filtresActifs.length;
  }

  get filtresActifs(): FiltreActifItem[] {
    const v = this.form.value;
    const f: FiltreActifItem[] = [];
    if (v.statuts?.length)
      f.push({ label: `Statuts: ${v.statuts.join(', ')}`, retirer: () => this.form.patchValue({ statuts: [] }) });
    if (v.cleDefinition?.trim())
      f.push({ label: `Définition: ${v.cleDefinition}`, retirer: () => this.form.patchValue({ cleDefinition: '' }) });
    if (v.aggregateId)
      f.push({ label: `Aggregate: ${v.aggregateId}`, retirer: () => this.form.patchValue({ aggregateId: null }) });
    if (v.idNoeudCourant?.trim())
      f.push({ label: `Nœud: ${v.idNoeudCourant}`, retirer: () => this.form.patchValue({ idNoeudCourant: '' }) });
    if (v.dateDebutMin)
      f.push({ label: `Depuis: ${this.formatDate(v.dateDebutMin)}`, retirer: () => this.form.patchValue({ dateDebutMin: null }) });
    if (v.dateDebutMax)
      f.push({ label: `Jusqu'à: ${this.formatDate(v.dateDebutMax)}`, retirer: () => this.form.patchValue({ dateDebutMax: null }) });
    if (v.racinesSeulement)
      f.push({ label: 'Racines seulement', retirer: () => this.form.patchValue({ racinesSeulement: false }) });
    this.filtresVariablesArray.controls.forEach((ctrl, i) => {
      const fv = ctrl.value as FiltreVariable;
      if (fv.nom?.trim()) {
        const opLabel = OPERATEUR_LABELS[fv.operateur] ?? fv.operateur;
        f.push({ label: `${fv.nom} ${opLabel} "${fv.valeur}"`, retirer: () => this.supprimerFiltreVariable(i) });
      }
    });
    return f;
  }

  // ── Formulaire ───────────────────────────────────────────────────────────────

  readonly form: FormGroup = this.fb.group({
    statuts:          [[] as StatutInstance[]],
    cleDefinition:    [''],
    aggregateId:      [null as number | null],
    idNoeudCourant:   [''],
    dateDebutMin:     [null as Date | null],
    dateDebutMax:     [null as Date | null],
    racinesSeulement: [false],
    filtresVariables: this.fb.array([]),
  });

  get filtresVariablesArray(): FormArray {
    return this.form.get('filtresVariables') as FormArray;
  }

  ajouterFiltreVariable(): void {
    this.filtresVariablesArray.push(
      this.fb.group({ nom: [''], operateur: ['Contient' as OperateurVariable], valeur: [''] }),
      { emitEvent: false }
    );
  }

  supprimerFiltreVariable(i: number): void {
    this.filtresVariablesArray.removeAt(i);
    this.page = 1;
  }

  // ── Cycle de vie ─────────────────────────────────────────────────────────────

  ngOnInit(): void {
    this.restaurerDepuisUrl();

    // Déclenche une recherche dès que le formulaire change (avec debounce)
    this.form.valueChanges.pipe(
      debounceTime(DEBOUNCE_MS),
      distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
      takeUntil(this.destroy$)
    ).subscribe(() => {
      this.page = 1;
      this.rechercher();
    });

    this.rechercher();
  }

  onSort(s: Sort): void {
    this.triColonne = s.direction ? s.active : undefined;
    this.triSens    = (s.direction || 'desc') as 'asc' | 'desc';
    this.page = 1;
    this.rechercher();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Recherche ────────────────────────────────────────────────────────────────

  rechercher(): void {
    this.loading = true;
    const v = this.form.value;

    const filtresVariables = (this.filtresVariablesArray.value as FiltreVariable[])
      .filter(f => f.nom?.trim());

    const query: RechercheInstancesQuery = {
      statuts:          v.statuts?.length ? v.statuts : undefined,
      cleDefinition:    v.cleDefinition?.trim() || undefined,
      aggregateId:      v.aggregateId || undefined,
      idNoeudCourant:   v.idNoeudCourant?.trim() || undefined,
      dateDebutMin:     v.dateDebutMin ? (v.dateDebutMin as Date).toISOString() : undefined,
      dateDebutMax:     v.dateDebutMax ? (v.dateDebutMax as Date).toISOString() : undefined,
      racinesSeulement: v.racinesSeulement || undefined,
      filtresVariables: filtresVariables.length ? filtresVariables : undefined,
      page:             this.page,
      taille:           this.taille,
      triColonne:       this.triColonne,
      triSens:          this.triSens,
    };

    this.bpm.rechercherInstances(query).subscribe({
      next: r => { this.resultat = r; this.loading = false; },
      error: () => { this.loading = false; },
    });
  }

  onPage(e: PageEvent): void {
    this.page   = e.pageIndex + 1;
    this.taille = e.pageSize;
    this.rechercher();
  }

  reinitialiser(): void {
    while (this.filtresVariablesArray.length) {
      this.filtresVariablesArray.removeAt(0, { emitEvent: false });
    }
    this.form.reset({ statuts: [], racinesSeulement: false, filtresVariables: [] });
    this.page = 1; this.triColonne = undefined; this.triSens = 'desc';
    this.rechercher();
  }

  appliquerFiltreRapide(statut: StatutInstance): void {
    this.form.patchValue({ statuts: [statut] });
  }

  // ── Helpers ──────────────────────────────────────────────────────────────────

  labelStatut(s: StatutInstance): string { return STATUTS_LABELS[s] ?? s; }

  formatDate(d: Date | string | null): string {
    if (!d) return '';
    return new Date(d).toLocaleDateString('fr-FR');
  }

  naviguer(id: number): void {
    this.router.navigate(['/instances', id]);
  }

  private restaurerDepuisUrl(): void {
    const qs = this.route.snapshot.queryParams;
    const patch: Partial<typeof this.form.value> = {};
    if (qs['statut'])        patch['statuts']       = [qs['statut'] as StatutInstance];
    if (qs['cleDefinition']) patch['cleDefinition'] = qs['cleDefinition'];
    if (qs['aggregateId'])   patch['aggregateId']   = Number(qs['aggregateId']);
    if (Object.keys(patch).length) {
      this.form.patchValue(patch, { emitEvent: false });
    }
  }
}
