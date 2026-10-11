export class MermaidLabel {
    constructor() {
        this.text = '';
        this.x = 0;
        this.y = 0;
    }

    text!: string;
    x!: number;
    y!: number;

    toString(): string {
        return 'eQuantic.UI.Components.MermaidLabel';
    }
}

