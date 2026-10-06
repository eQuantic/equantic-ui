import { MermaidArrowhead, MermaidCurve, MermaidLabel, MermaidPlacedNode, MermaidSegment } from "../runtime-exports";

export class MermaidScene {
    constructor() {
        this.width = 0;
        this.height = 0;
        this.nodes = [];
        this.segments = [];
        this.curves = [];
        this.arrows = [];
        this.labels = [];
    }

    declare width: number;
    declare height: number;
    declare nodes: MermaidPlacedNode[];
    declare segments: MermaidSegment[];
    declare curves: MermaidCurve[];
    declare arrows: MermaidArrowhead[];
    declare labels: MermaidLabel[];
}

