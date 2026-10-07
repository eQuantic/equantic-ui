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

    width!: number;
    height!: number;
    nodes!: MermaidPlacedNode[];
    segments!: MermaidSegment[];
    curves!: MermaidCurve[];
    arrows!: MermaidArrowhead[];
    labels!: MermaidLabel[];
}

