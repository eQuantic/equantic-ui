import { MermaidArrowhead, MermaidLabel, MermaidPlacedNode, MermaidSegment } from "../runtime-exports";
export class MermaidScene {
    constructor(props?: any) {  if (props && typeof props === 'object') Object.assign(this, props); }
    width: number = 0;
    height: number = 0;
    nodes: MermaidPlacedNode[] = [];
    segments: MermaidSegment[] = [];
    arrows: MermaidArrowhead[] = [];
    labels: MermaidLabel[] = [];
}

