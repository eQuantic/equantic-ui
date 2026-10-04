export class MermaidNodeRef {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    id: string = '';
    label: string = '';
    shape: string = 'rect';
    shaped: boolean = false;
    end: number = 0;
}

