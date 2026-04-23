export type StatutInstance = 'Active' | 'Suspendue' | 'EnErreur' | 'Terminee';

export type TypeEvenement =
  | 'DebutProcessus'
  | 'EntreeNoeud'
  | 'SortieNoeud'
  | 'NoeudSuspendu'
  | 'NoeudRepris'
  | 'ErreurNoeud'
  | 'FinProcessus'
  | 'MigrationInstance'
  | 'SignalRecu'
  | 'VariableModifiee'
  | 'TacheAssignee';

export type ResultatEvenement = 'Succes' | 'Erreur' | 'Suspendu';

export interface InstanceProcessus {
  id: number;
  cleDefinition: string;
  versionDefinition: number;
  aggregateId: number;
  statut: StatutInstance;
  idNoeudCourant?: string;
  idInstanceParent?: number;
  dateDebut: string;
  dateFin?: string;
  dateCreation: string;
  dateMaj: string;
}

export interface EvenementInstance {
  id: number;
  idInstance: number;
  typeEvenement: TypeEvenement;
  idNoeud?: string;
  nomNoeud?: string;
  horodatage: string;
  dureeMs?: number;
  resultat?: ResultatEvenement;
  detail?: string;
}

export interface InstanceEchue {
  idInstance: number;
  dateEcheance: string;
}

// ── Recherche avancée ─────────────────────────────────────────────────────────

export interface RechercheInstancesQuery {
  statuts?:          StatutInstance[];
  cleDefinition?:    string;
  aggregateId?:      number;
  idNoeudCourant?:   string;
  dateDebutMin?:     string;   // ISO date string
  dateDebutMax?:     string;
  racinesSeulement?: boolean;
  nomVariable?:      string;
  valeurVariable?:   string;
  page:              number;
  taille:            number;
  triColonne?:       string;
  triSens?:          'asc' | 'desc';
}

export interface ResultatRechercheInstances {
  total:      number;
  page:       number;
  taille:     number;
  totalPages: number;
  instances:  InstanceProcessus[];
}

export interface ResultatMigration {
  idInstance: number;
  succes: boolean;
  ancienneVersion: number;
  nouvelleVersion: number;
  ancienNoeudId?: string;
  nouveauNoeudId?: string;
  messageErreur?: string;
}

export interface DashboardStats {
  definitions: {
    total: number;
    publiees: number;
    brouillons: number;
  };
  instances: {
    actives: number;
    suspendues: number;
    enErreur: number;
    terminees: number;
    echues: number;
    total: number;
  };
}
