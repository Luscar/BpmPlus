export type TypeNoeud =
  | 'NoeudMetier'
  | 'NoeudInteractif'
  | 'NoeudDecision'
  | 'NoeudAttenteTemps'
  | 'NoeudAttenteSignal'
  | 'NoeudSousProcessus';

export type StatutDefinition = 'Brouillon' | 'Publiee';

export interface FluxSortant {
  vers: string;
  estParDefaut?: boolean;
  condition?: {
    type: string;
    nomVariable?: string;
    operateur?: string;
    valeur?: unknown;
    nomQuery?: string;
  };
}

export interface NoeudProcessus {
  id: string;
  nom: string;
  type: TypeNoeud;
  estFinale: boolean;
  fluxSortants: FluxSortant[];
  // NoeudMetier
  nomCommande?: string;
  // NoeudInteractif
  definitionTache?: { titre: string; description?: string; type?: string };
  // NoeudAttenteSignal
  nomSignal?: string;
  // NoeudSousProcessus
  cleDefinition?: string;
  version?: number;
}

export interface DefinitionProcessus {
  id: number;
  cle: string;
  version: number;
  nom: string;
  statut: StatutDefinition;
  noeudDebut: string;
  noeuds: NoeudProcessus[];
  dateCreation: string;
  datePublication?: string;
}

export interface DefinitionResume {
  id: number;
  cle: string;
  version: number;
  nom: string;
  statut: StatutDefinition;
  dateCreation: string;
  datePublication?: string;
  nombreNoeuds: number;
}
