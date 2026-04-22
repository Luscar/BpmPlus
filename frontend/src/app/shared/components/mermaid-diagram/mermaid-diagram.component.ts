import {
  AfterViewInit,
  Component,
  ElementRef,
  Input,
  OnChanges,
  ViewChild,
  inject,
} from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import mermaid from 'mermaid';

let mermaidInitialized = false;

@Component({
  selector: 'app-mermaid-diagram',
  standalone: true,
  imports: [MatProgressSpinnerModule],
  template: `
    <div class="mermaid-wrapper">
      @if (loading) {
        <div class="loading-overlay">
          <mat-spinner diameter="40" />
        </div>
      }
      <div #container class="mermaid-container"></div>
    </div>
  `,
  styles: [`
    .mermaid-wrapper {
      position: relative;
      width: 100%;
      min-height: 120px;
      overflow-x: auto;
    }
    .loading-overlay {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 120px;
    }
    .mermaid-container {
      width: 100%;
    }
    .mermaid-container :host ::ng-deep svg {
      width: 100%;
      height: auto;
    }
  `],
})
export class MermaidDiagramComponent implements AfterViewInit, OnChanges {
  @Input({ required: true }) diagram!: string;
  @ViewChild('container') containerRef!: ElementRef<HTMLDivElement>;

  loading = true;
  private diagramId = `bpm-${Math.random().toString(36).slice(2)}`;

  ngAfterViewInit(): void {
    this.renderDiagram();
  }

  ngOnChanges(): void {
    if (this.containerRef) {
      this.renderDiagram();
    }
  }

  private async renderDiagram(): Promise<void> {
    if (!this.diagram) return;
    this.loading = true;

    if (!mermaidInitialized) {
      mermaid.initialize({
        startOnLoad: false,
        theme: 'base',
        flowchart: { htmlLabels: true, curve: 'linear' },
        securityLevel: 'loose',
      });
      mermaidInitialized = true;
    }

    try {
      const { svg } = await mermaid.render(this.diagramId, this.diagram);
      this.containerRef.nativeElement.innerHTML = svg;
      // Reuse a new ID on next render to avoid Mermaid caching stale SVGs
      this.diagramId = `bpm-${Math.random().toString(36).slice(2)}`;
    } catch {
      this.containerRef.nativeElement.innerHTML =
        '<p class="error">Erreur de rendu du diagramme.</p>';
    } finally {
      this.loading = false;
    }
  }
}
