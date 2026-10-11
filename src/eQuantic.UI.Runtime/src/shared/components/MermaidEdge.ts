export class MermaidEdge {
    constructor() {
        this.from = '';
        this.to = '';
        this.label = '';
        this.arrow = true;
    }

    from!: string;
    to!: string;
    label!: string;
    arrow!: boolean;

    toString(): string {
        return 'eQuantic.UI.Components.MermaidEdge';
    }
}

