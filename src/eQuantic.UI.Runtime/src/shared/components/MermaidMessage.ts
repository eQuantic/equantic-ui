export class MermaidMessage {
    constructor() {
        this.from = '';
        this.to = '';
        this.label = '';
        this.dashed = false;
    }

    declare from: string;
    declare to: string;
    declare label: string;
    declare dashed: boolean;
}

