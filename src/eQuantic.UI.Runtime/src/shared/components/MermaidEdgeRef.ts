export class MermaidEdgeRef {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    arrow: boolean = false;
    label: string = '';
    end: number = 0;
}

