export class MarkdownBulletMatch {
    constructor() {
        this.marker = '•';
        this.content = '';
    }

    marker!: string;
    content!: string;

    toString(): string {
        return 'eQuantic.UI.Components.MarkdownBulletMatch';
    }
}

