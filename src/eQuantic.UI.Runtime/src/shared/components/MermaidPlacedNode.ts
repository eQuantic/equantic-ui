import { MermaidNode } from "../runtime-exports";

export class MermaidPlacedNode {
    constructor() {
        this.node = new MermaidNode();
        this.x = 0;
        this.y = 0;
        this.w = 0;
        this.h = 0;
    }

    node!: MermaidNode;
    x!: number;
    y!: number;
    w!: number;
    h!: number;

    toString(): string {
        return 'eQuantic.UI.Components.MermaidPlacedNode';
    }
}

