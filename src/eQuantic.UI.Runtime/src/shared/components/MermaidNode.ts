export class MermaidNode {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    id: string = '';
    label: string = '';
    shape: string = 'rect';
}

