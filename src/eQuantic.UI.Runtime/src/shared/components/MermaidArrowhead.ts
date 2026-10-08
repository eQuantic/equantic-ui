export class MermaidArrowhead {
    constructor() {
        this.x = 0;
        this.y = 0;
        this.direction = 0;
    }

    x!: number;
    y!: number;
    direction!: number;

    toString(): string {
        return 'eQuantic.UI.Components.MermaidArrowhead';
    }
}

