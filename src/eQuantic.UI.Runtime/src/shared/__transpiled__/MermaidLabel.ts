export class MermaidLabel {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    text: string = '';
    x: number = 0;
    y: number = 0;
}

