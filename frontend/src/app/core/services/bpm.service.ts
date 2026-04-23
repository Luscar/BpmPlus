import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { DefinitionProcessus, DefinitionResume } from '../models/definition.model';
import {
  DashboardStats,
  EvenementInstance,
  InstanceEchue,
  InstanceProcessus,
  RechercheInstancesQuery,
  ResultatMigration,
  ResultatRechercheInstances,
  StatutInstance,
} from '../models/instance.model';

@Injectable({ providedIn: 'root' })
export class BpmService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';

  // ── Dashboard ──────────────────────────────────────────────────────────────

  getDashboardStats(): Observable<DashboardStats> {
    return this.http.get<DashboardStats>(`${this.base}/dashboard/stats`);
  }

  // ── Définitions ───────────────────────────────────────────────────────────

  getDefinitions(): Observable<DefinitionResume[]> {
    return this.http.get<DefinitionResume[]>(`${this.base}/definitions`);
  }

  getDefinitionVersions(cle: string): Observable<DefinitionProcessus[]> {
    return this.http.get<DefinitionProcessus[]>(`${this.base}/definitions/${cle}`);
  }

  getDefinitionVersion(cle: string, version: number): Observable<DefinitionProcessus> {
    return this.http.get<DefinitionProcessus>(`${this.base}/definitions/${cle}/v/${version}`);
  }

  publierDefinition(cle: string): Observable<void> {
    return this.http.post<void>(`${this.base}/definitions/${cle}/publier`, {});
  }

  // ── Instances ─────────────────────────────────────────────────────────────

  getInstances(statut?: StatutInstance, cleDefinition?: string): Observable<InstanceProcessus[]> {
    let params = new HttpParams();
    if (statut) params = params.set('statut', statut);
    if (cleDefinition) params = params.set('cleDefinition', cleDefinition);
    return this.http.get<InstanceProcessus[]>(`${this.base}/instances`, { params });
  }

  rechercherInstances(q: RechercheInstancesQuery): Observable<ResultatRechercheInstances> {
    // On utilise POST pour transmettre la liste multi-valeur des statuts sans encoder manuellement
    return this.http.post<ResultatRechercheInstances>(
      `${this.base}/instances/recherche`,
      q
    );
  }

  getInstance(id: number): Observable<InstanceProcessus> {
    return this.http.get<InstanceProcessus>(`${this.base}/instances/${id}`);
  }

  getHistorique(id: number): Observable<EvenementInstance[]> {
    return this.http.get<EvenementInstance[]>(`${this.base}/instances/${id}/historique`);
  }

  getVariables(id: number): Observable<Record<string, unknown>> {
    return this.http.get<Record<string, unknown>>(`${this.base}/instances/${id}/variables`);
  }

  getEnfants(id: number): Observable<InstanceProcessus[]> {
    return this.http.get<InstanceProcessus[]>(`${this.base}/instances/${id}/enfants`);
  }

  getSignaux(id: number): Observable<string[]> {
    return this.http.get<string[]>(`${this.base}/instances/${id}/signaux`);
  }

  getTache(id: number): Observable<{ idTache?: number; logon?: string }> {
    return this.http.get<{ idTache?: number; logon?: string }>(
      `${this.base}/instances/${id}/tache`
    );
  }

  getInstancesEchues(): Observable<InstanceEchue[]> {
    return this.http.get<InstanceEchue[]>(`${this.base}/instances/echues`);
  }

  // ── Actions ───────────────────────────────────────────────────────────────

  demarrerInstance(
    cleDefinition: string,
    aggregateId: number,
    variables?: Record<string, unknown>
  ): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(`${this.base}/instances`, {
      cleDefinition,
      aggregateId,
      variables,
    });
  }

  terminerEtape(id: number): Observable<void> {
    return this.http.post<void>(`${this.base}/instances/${id}/terminer-etape`, {});
  }

  envoyerSignal(id: number, nomSignal: string): Observable<void> {
    return this.http.post<void>(`${this.base}/instances/${id}/signal`, { nomSignal });
  }

  reprendreTimer(id: number): Observable<void> {
    return this.http.post<void>(`${this.base}/instances/${id}/reprendre-timer`, {});
  }

  assigner(id: number, logon: string): Observable<void> {
    return this.http.post<void>(`${this.base}/instances/${id}/assigner`, { logon });
  }

  modifierVariable(id: number, nom: string, valeur: unknown): Observable<void> {
    return this.http.put<void>(`${this.base}/instances/${id}/variables/${nom}`, { valeur });
  }

  // ── Migration ─────────────────────────────────────────────────────────────

  migrerInstance(
    id: number,
    versionCible: number,
    mappingNoeuds?: Record<string, string>
  ): Observable<ResultatMigration> {
    return this.http.post<ResultatMigration>(
      `${this.base}/instances/${id}/migrer`,
      { versionCible, mappingNoeuds }
    );
  }

  migrerToutesInstances(
    cle: string,
    versionCible: number,
    mappingNoeuds?: Record<string, string>
  ): Observable<ResultatMigration[]> {
    return this.http.post<ResultatMigration[]>(
      `${this.base}/definitions/${cle}/migrer-instances`,
      { versionCible, mappingNoeuds }
    );
  }
}
