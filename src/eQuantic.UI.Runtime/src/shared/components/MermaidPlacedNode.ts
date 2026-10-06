import { MermaidNode } from "../runtime-exports";

export class MermaidPlacedNode {
    constructor() {
        this.node = new MermaidNode();
        this.x = 0;
        this.y = 0;
        this.w = 0;
        this.h = 0;
    }

    declare node: MermaidNode;
    declare x: number;
    declare y: number;
    declare w: number;
    declare h: number;
}

