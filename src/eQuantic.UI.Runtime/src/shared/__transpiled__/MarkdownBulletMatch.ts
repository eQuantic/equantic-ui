export class MarkdownBulletMatch {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    marker: string = '•';
    content: string = '';
}

