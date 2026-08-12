import { MermaidNode } from "@equantic/runtime";
export class MermaidPlacedNode {
    constructor(props?: any) {  if (props && typeof props === 'object') Object.assign(this, props); }
    node: MermaidNode = new MermaidNode();
    x: number = 0;
    y: number = 0;
    w: number = 0;
    h: number = 0;
}

