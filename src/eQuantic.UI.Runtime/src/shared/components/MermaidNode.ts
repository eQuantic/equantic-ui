export class MermaidNode {
    constructor() {
        this.id = '';
        this.label = '';
        this.shape = 'rect';
    }

    id!: string;
    label!: string;
    shape!: string;

    toString(): string {
        return 'eQuantic.UI.Components.MermaidNode';
    }
}

