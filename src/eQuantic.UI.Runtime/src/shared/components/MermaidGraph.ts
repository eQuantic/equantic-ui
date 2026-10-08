import { MermaidEdge, MermaidMessage, MermaidNode } from "../runtime-exports";

export class MermaidGraph {
    constructor() {
        this.kind = 'flowchart';
        this.vertical = true;
        this.nodes = [];
        this.edges = [];
        this.messages = [];
    }

    kind!: string;
    vertical!: boolean;
    nodes!: MermaidNode[];
    edges!: MermaidEdge[];
    messages!: MermaidMessage[];

    toString(): string {
        return 'eQuantic.UI.Components.MermaidGraph';
    }
}

