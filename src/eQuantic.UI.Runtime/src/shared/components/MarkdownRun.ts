export class MarkdownRun {
    constructor() {
        this.text = '';
        this.bold = false;
        this.italic = false;
        this.code = false;
        this.href = '';
    }

    declare text: string;
    declare bold: boolean;
    declare italic: boolean;
    declare code: boolean;
    declare href: string;

    get isLink(): boolean {
        return this.href.length > 0;
    }
}

