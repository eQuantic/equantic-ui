export class MarkdownLinkMatch {
    constructor(props?: any) {  if (props && typeof props === 'object') Object.assign(this, props); }
    label: string = '';
    href: string = '';
    end: number = 0;
}

