export class MermaidEdgeRef {
    constructor() {
        this.arrow = false;
        this.label = '';
        this.end = 0;
    }

    arrow!: boolean;
    label!: string;
    end!: number;

    toString(): string {
        return 'eQuantic.UI.Components.MermaidEdgeRef';
    }
}

