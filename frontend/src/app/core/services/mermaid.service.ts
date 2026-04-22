import { Injectable } from '@angular/core';
import { DefinitionProcessus, NoeudProcessus } from '../models/definition.model';

@Injectable({ providedIn: 'root' })
export class MermaidService {
  generateDiagram(definition: DefinitionProcessus, currentNodeId?: string): string {
    const lines: string[] = ['flowchart LR'];

    lines.push(
      '  classDef metier fill:#3f51b5,stroke:#283593,color:#fff',
      '  classDef interactif fill:#2e7d32,stroke:#1b5e20,color:#fff',
      '  classDef decision fill:#e65100,stroke:#bf360c,color:#fff',
      '  classDef attenteTemps fill:#6a1b9a,stroke:#4a148c,color:#fff',
      '  classDef attenteSignal fill:#00838f,stroke:#006064,color:#fff',
      '  classDef sousProcessus fill:#37474f,stroke:#263238,color:#fff',
      '  classDef courant fill:#c62828,stroke:#b71c1c,color:#fff,stroke-width:4px',
      '  classDef final fill:#4e342e,stroke:#3e2723,color:#fff'
    );

    for (const noeud of definition.noeuds) {
      const safeId = this.safeId(noeud.id);
      const label = this.escapeLabel(noeud.nom || noeud.id);
      const shape = this.nodeShape(noeud.type, label);
      lines.push(`  ${safeId}${shape}`);
    }

    for (const noeud of definition.noeuds) {
      const from = this.safeId(noeud.id);
      for (const flux of noeud.fluxSortants) {
        const to = this.safeId(flux.vers);
        const edgeLabel = this.fluxLabel(noeud, flux.estParDefaut, flux.condition);
        lines.push(
          edgeLabel
            ? `  ${from} -->|"${edgeLabel}"| ${to}`
            : `  ${from} --> ${to}`
        );
      }
    }

    for (const noeud of definition.noeuds) {
      const safeId = this.safeId(noeud.id);
      const isCourant = currentNodeId && noeud.id === currentNodeId;
      const cls = isCourant ? 'courant' : this.nodeClass(noeud.type, noeud.estFinale);
      lines.push(`  class ${safeId} ${cls}`);
    }

    return lines.join('\n');
  }

  private safeId(id: string): string {
    return id.replace(/[-\s.]/g, '_');
  }

  private escapeLabel(label: string): string {
    return label.replace(/"/g, "'").replace(/[<>{}[\]]/g, ' ');
  }

  private nodeShape(type: NoeudProcessus['type'], label: string): string {
    switch (type) {
      case 'NoeudInteractif':    return `([${label}])`;
      case 'NoeudDecision':      return `{${label}}`;
      case 'NoeudAttenteTemps':  return `[[${label}]]`;
      case 'NoeudAttenteSignal': return `((${label}))`;
      case 'NoeudSousProcessus': return `[/${label}/]`;
      default:                   return `[${label}]`;
    }
  }

  private nodeClass(type: NoeudProcessus['type'], estFinale: boolean): string {
    if (estFinale) return 'final';
    switch (type) {
      case 'NoeudMetier':        return 'metier';
      case 'NoeudInteractif':    return 'interactif';
      case 'NoeudDecision':      return 'decision';
      case 'NoeudAttenteTemps':  return 'attenteTemps';
      case 'NoeudAttenteSignal': return 'attenteSignal';
      case 'NoeudSousProcessus': return 'sousProcessus';
      default:                   return 'metier';
    }
  }

  private fluxLabel(
    noeud: NoeudProcessus,
    estParDefaut?: boolean,
    condition?: NoeudProcessus['fluxSortants'][number]['condition']
  ): string {
    if (estParDefaut) return 'défaut';
    if (!condition) return '';
    if (condition.type === 'ConditionVariable') {
      const op = this.operateurLabel(condition.operateur);
      return `${condition.nomVariable} ${op} ${condition.valeur}`;
    }
    if (condition.type === 'ConditionQuery') return condition.nomQuery ?? '';
    return '';
  }

  private operateurLabel(op?: string): string {
    const map: Record<string, string> = {
      Egal: '=', Different: '≠', Superieur: '>',
      Inferieur: '<', SuperieurOuEgal: '≥', InferieurOuEgal: '≤', Contient: '∋',
    };
    return op ? (map[op] ?? op) : '?';
  }
}
