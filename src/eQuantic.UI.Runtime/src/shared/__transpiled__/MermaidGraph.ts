import { MermaidEdge, MermaidMessage, MermaidNode } from "@equantic/runtime";
export class MermaidGraph {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }
    declare kind: string;
    vertical: boolean = true;
    nodes: MermaidNode[] = [];
    edges: MermaidEdge[] = [];
    messages: MermaidMessage[] = [];
}

