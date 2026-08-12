export class MarkdownRun {
    constructor(props?: any) {  if (props && typeof props === 'object') Object.assign(this, props); }
    text: string = '';
    bold: boolean = false;
    italic: boolean = false;
    code: boolean = false;
    href: string = '';
    get isLink(): boolean { return this.href.length > 0; }
}

