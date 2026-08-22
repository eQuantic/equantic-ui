export class MermaidSegment {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    x: number = 0;
    y: number = 0;
    w: number = 0;
    h: number = 0;
}

