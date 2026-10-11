export class MermaidNodeRef {
    constructor() {
        this.id = '';
        this.label = '';
        this.shape = 'rect';
        this.shaped = false;
        this.end = 0;
    }

    id!: string;
    label!: string;
    shape!: string;
    shaped!: boolean;
    end!: number;

    toString(): string {
        return 'eQuantic.UI.Components.MermaidNodeRef';
    }
}

