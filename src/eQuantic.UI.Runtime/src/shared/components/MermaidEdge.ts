export class MermaidEdge {
    constructor() {
        this.from = '';
        this.to = '';
        this.label = '';
        this.arrow = true;
    }

    declare from: string;
    declare to: string;
    declare label: string;
    declare arrow: boolean;
}

