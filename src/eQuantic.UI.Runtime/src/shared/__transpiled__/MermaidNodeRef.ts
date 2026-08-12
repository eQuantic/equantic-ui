export class MermaidNodeRef {
    constructor(props?: any) {  if (props && typeof props === 'object') Object.assign(this, props); }
    id: string = '';
    label: string = '';
    declare shape: string;
    shaped: boolean = false;
    end: number = 0;
}

