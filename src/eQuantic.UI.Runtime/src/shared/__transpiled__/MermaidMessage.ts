export class MermaidMessage {
    constructor(props?: any) {  if (props && typeof props === 'object') Object.assign(this, props); }
    from: string = '';
    to: string = '';
    label: string = '';
    dashed: boolean = false;
}

