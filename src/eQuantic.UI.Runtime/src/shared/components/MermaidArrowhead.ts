export class MermaidArrowhead {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }
    x: number = 0;
    y: number = 0;
    direction: number = 0;
}

