export class MermaidEdge {
    constructor(props?: any) {  if (props && typeof props === 'object') Object.assign(this, props); }
    from: string = '';
    to: string = '';
    label: string = '';
    arrow: boolean = true;
}

