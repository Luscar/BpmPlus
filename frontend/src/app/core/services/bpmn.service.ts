import { Injectable } from '@angular/core';
import { DefinitionProcessus, NoeudProcessus } from '../models/definition.model';

interface Pos { x: number; y: number; w: number; h: number; }

@Injectable({ providedIn: 'root' })
export class BpmnService {
  generateDiagram(def: DefinitionProcessus): string {
    return this.buildXml(def, this.computeLayout(def));
  }

  private computeLayout(def: DefinitionProcessus): Map<string, Pos> {
    const layerOf = new Map<string, number>();
    const queue: string[] = [def.noeudDebut];
    layerOf.set(def.noeudDebut, 0);
    while (queue.length) {
      const id = queue.shift()!;
      const node = def.noeuds.find(n => n.id === id);
      if (!node) continue;
      const l = layerOf.get(id)!;
      for (const flux of node.fluxSortants) {
        if (!layerOf.has(flux.vers)) {
          layerOf.set(flux.vers, l + 1);
          queue.push(flux.vers);
        }
      }
    }
    for (const n of def.noeuds) {
      if (!layerOf.has(n.id)) layerOf.set(n.id, 0);
    }

    const byLayer = new Map<number, string[]>();
    for (const [id, l] of layerOf) {
      if (!byLayer.has(l)) byLayer.set(l, []);
      byLayer.get(l)!.push(id);
    }

    const positions = new Map<string, Pos>();
    for (const [l, ids] of byLayer) {
      ids.forEach((id, i) => {
        const node = def.noeuds.find(n => n.id === id)!;
        const { w, h } = this.nodeSize(node, def);
        const cx = 100 + l * 220;
        const cy = 200 + (i - (ids.length - 1) / 2) * 120;
        positions.set(id, { x: Math.round(cx - w / 2), y: Math.round(cy - h / 2), w, h });
      });
    }
    return positions;
  }

  private nodeSize(node: NoeudProcessus, def: DefinitionProcessus): { w: number; h: number } {
    if (node.id === def.noeudDebut || node.estFinale) return { w: 36, h: 36 };
    if (node.type === 'NoeudDecision') return { w: 50, h: 50 };
    if (node.type === 'NoeudAttenteTemps' || node.type === 'NoeudAttenteSignal') return { w: 36, h: 36 };
    return { w: 100, h: 80 };
  }

  private bpmnTag(node: NoeudProcessus, def: DefinitionProcessus): string {
    if (node.id === def.noeudDebut) return 'startEvent';
    if (node.estFinale) return 'endEvent';
    switch (node.type) {
      case 'NoeudMetier': return 'serviceTask';
      case 'NoeudInteractif': return 'userTask';
      case 'NoeudDecision': return 'exclusiveGateway';
      case 'NoeudAttenteTemps':
      case 'NoeudAttenteSignal': return 'intermediateCatchEvent';
      case 'NoeudSousProcessus': return 'callActivity';
      default: return 'task';
    }
  }

  private safeId(id: string): string {
    return 'n_' + id.replace(/[^a-zA-Z0-9_]/g, '_');
  }

  private esc(s: string): string {
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  private fluxLabel(node: NoeudProcessus, flux: NoeudProcessus['fluxSortants'][number]): string {
    if (flux.estParDefaut) return 'défaut';
    if (!flux.condition) return '';
    const c = flux.condition;
    if (c.type === 'ConditionVariable') {
      const ops: Record<string, string> = {
        Egal: '=', Different: '≠', Superieur: '>', Inferieur: '<',
        SuperieurOuEgal: '≥', InferieurOuEgal: '≤', Contient: '∋',
      };
      return `${c.nomVariable} ${ops[c.operateur ?? ''] ?? c.operateur ?? '?'} ${c.valeur}`;
    }
    if (c.type === 'ConditionQuery') return c.nomQuery ?? '';
    return '';
  }

  private buildXml(def: DefinitionProcessus, layout: Map<string, Pos>): string {
    const pid = 'P_' + def.cle.replace(/[^a-zA-Z0-9_]/g, '_');
    const elements: string[] = [];
    const shapes: string[] = [];
    const edges: string[] = [];
    let fi = 0;

    for (const node of def.noeuds) {
      const nid = this.safeId(node.id);
      const tag = this.bpmnTag(node, def);
      const name = this.esc(node.nom || node.id);
      const pos = layout.get(node.id)!;

      if (tag === 'intermediateCatchEvent') {
        const inner = node.type === 'NoeudAttenteTemps'
          ? `<bpmn:timerEventDefinition id="${nid}_evDef"/>`
          : `<bpmn:signalEventDefinition id="${nid}_evDef"/>`;
        elements.push(`    <bpmn:${tag} id="${nid}" name="${name}">${inner}</bpmn:${tag}>`);
      } else {
        elements.push(`    <bpmn:${tag} id="${nid}" name="${name}"/>`);
      }

      const marker = tag === 'exclusiveGateway' ? ' isMarkerVisible="true"' : '';
      shapes.push(
        `      <bpmndi:BPMNShape id="${nid}_di" bpmnElement="${nid}"${marker}>` +
        `\n        <dc:Bounds x="${pos.x}" y="${pos.y}" width="${pos.w}" height="${pos.h}"/>` +
        `\n        <bpmndi:BPMNLabel/>\n      </bpmndi:BPMNShape>`
      );

      for (const flux of node.fluxSortants) {
        const fid = `flow_${fi++}`;
        const tid = this.safeId(flux.vers);
        const label = this.fluxLabel(node, flux);
        const nameAttr = label ? ` name="${this.esc(label)}"` : '';
        elements.push(`    <bpmn:sequenceFlow id="${fid}" sourceRef="${nid}" targetRef="${tid}"${nameAttr}/>`);

        const tp = layout.get(flux.vers);
        if (pos && tp) {
          const sx = pos.x + pos.w, sy = pos.y + Math.round(pos.h / 2);
          const tx = tp.x,          ty = tp.y + Math.round(tp.h / 2);
          edges.push(
            `      <bpmndi:BPMNEdge id="${fid}_di" bpmnElement="${fid}">` +
            `\n        <di:waypoint x="${sx}" y="${sy}"/>` +
            `\n        <di:waypoint x="${tx}" y="${ty}"/>` +
            `\n      </bpmndi:BPMNEdge>`
          );
        }
      }
    }

    return [
      `<?xml version="1.0" encoding="UTF-8"?>`,
      `<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"`,
      `                  xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI"`,
      `                  xmlns:dc="http://www.omg.org/spec/DD/20100524/DC"`,
      `                  xmlns:di="http://www.omg.org/spec/DD/20100524/DI"`,
      `                  id="Definitions_1" targetNamespace="http://bpmn.io/schema/bpmn">`,
      `  <bpmn:process id="${pid}" isExecutable="false">`,
      ...elements,
      `  </bpmn:process>`,
      `  <bpmndi:BPMNDiagram id="BPMNDiagram_1">`,
      `    <bpmndi:BPMNPlane id="BPMNPlane_1" bpmnElement="${pid}">`,
      ...shapes,
      ...edges,
      `    </bpmndi:BPMNPlane>`,
      `  </bpmndi:BPMNDiagram>`,
      `</bpmn:definitions>`,
    ].join('\n');
  }
}
