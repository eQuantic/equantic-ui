export class MermaidMessage {
    constructor() {
        this.from = '';
        this.to = '';
        this.label = '';
        this.dashed = false;
    }

    from!: string;
    to!: string;
    label!: string;
    dashed!: boolean;

    toString(): string {
        return 'eQuantic.UI.Components.MermaidMessage';
    }
}

