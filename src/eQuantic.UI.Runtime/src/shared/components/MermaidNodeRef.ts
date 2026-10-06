export class MermaidNodeRef {
    constructor() {
        this.id = '';
        this.label = '';
        this.shape = 'rect';
        this.shaped = false;
        this.end = 0;
    }

    declare id: string;
    declare label: string;
    declare shape: string;
    declare shaped: boolean;
    declare end: number;
}

