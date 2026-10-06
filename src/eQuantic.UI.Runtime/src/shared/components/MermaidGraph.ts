import { MermaidEdge, MermaidMessage, MermaidNode } from "../runtime-exports";

export class MermaidGraph {
    constructor() {
        this.kind = 'flowchart';
        this.vertical = true;
        this.nodes = [];
        this.edges = [];
        this.messages = [];
    }

    declare kind: string;
    declare vertical: boolean;
    declare nodes: MermaidNode[];
    declare edges: MermaidEdge[];
    declare messages: MermaidMessage[];
}

