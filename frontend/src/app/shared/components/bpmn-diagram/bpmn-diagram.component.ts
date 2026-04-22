import {
  AfterViewInit,
  Component,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  ViewChild,
} from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

@Component({
  selector: 'app-bpmn-diagram',
  standalone: true,
  imports: [MatProgressSpinnerModule],
  template: `
    <div class="bpmn-wrapper">
      @if (loading) {
        <div class="loading-overlay"><mat-spinner diameter="40"/></div>
      }
      <div #container class="bpmn-container"></div>
    </div>
  `,
  styles: [`
    .bpmn-wrapper { position: relative; width: 100%; min-height: 300px; overflow: hidden; }
    .loading-overlay { display: flex; align-items: center; justify-content: center; min-height: 300px; }
    .bpmn-container { width: 100%; height: 420px; }
    :host ::ng-deep .noeud-courant .djs-visual rect,
    :host ::ng-deep .noeud-courant .djs-visual circle,
    :host ::ng-deep .noeud-courant .djs-visual polygon {
      fill: #c62828 !important;
      stroke: #b71c1c !important;
    }
    :host ::ng-deep .noeud-courant .djs-visual text { fill: #fff !important; }
  `],
})
export class BpmnDiagramComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input({ required: true }) diagram!: string;
  @Input() currentNodeId?: string;
  @ViewChild('container') containerRef!: ElementRef<HTMLDivElement>;

  loading = true;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private viewer: any = null;
  private ready = false;

  async ngAfterViewInit(): Promise<void> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const { default: BpmnViewer } = await import('bpmn-js/lib/Viewer') as any;
    this.viewer = new BpmnViewer({ container: this.containerRef.nativeElement });
    this.ready = true;
    this.render();
  }

  ngOnChanges(): void {
    if (this.ready) this.render();
  }

  ngOnDestroy(): void {
    this.viewer?.destroy();
  }

  private async render(): Promise<void> {
    if (!this.diagram || !this.viewer) return;
    this.loading = true;
    try {
      await this.viewer.importXML(this.diagram);
      const canvas = this.viewer.get('canvas');
      canvas.zoom('fit-viewport');
      if (this.currentNodeId) {
        const sid = 'n_' + this.currentNodeId.replace(/[^a-zA-Z0-9_]/g, '_');
        try { canvas.addMarker(sid, 'noeud-courant'); } catch { /* node not in diagram */ }
      }
    } catch {
      this.containerRef.nativeElement.innerHTML =
        '<p style="padding:1rem;color:#c62828">Erreur de rendu BPMN.</p>';
    } finally {
      this.loading = false;
    }
  }
}
