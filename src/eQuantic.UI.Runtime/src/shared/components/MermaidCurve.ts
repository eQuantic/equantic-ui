export class MermaidCurve {
    constructor() {
        this.x = 0;
        this.y = 0;
        this.w = 0;
        this.h = 0;
        this.path = '';
        this.viewBox = '';
    }

    x!: number;
    y!: number;
    w!: number;
    h!: number;
    path!: string;
    viewBox!: string;

    toString(): string {
        return 'eQuantic.UI.Components.MermaidCurve';
    }
}

