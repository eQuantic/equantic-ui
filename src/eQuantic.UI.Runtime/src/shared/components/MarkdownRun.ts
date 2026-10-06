export class MarkdownRun {
    constructor() {
        this.text = '';
        this.bold = false;
        this.italic = false;
        this.code = false;
        this.href = '';
    }

    text!: string;
    bold!: boolean;
    italic!: boolean;
    code!: boolean;
    href!: string;

    get isLink(): boolean {
        return this.href.length > 0;
    }
}

