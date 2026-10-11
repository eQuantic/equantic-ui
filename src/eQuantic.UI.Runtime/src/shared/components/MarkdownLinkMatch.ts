export class MarkdownLinkMatch {
    constructor() {
        this.label = '';
        this.href = '';
        this.end = 0;
    }

    label!: string;
    href!: string;
    end!: number;

    toString(): string {
        return 'eQuantic.UI.Components.MarkdownLinkMatch';
    }
}

