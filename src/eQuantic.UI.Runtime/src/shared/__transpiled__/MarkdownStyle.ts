export class MarkdownStyle {
    constructor(props?: any) {  if (props && typeof props === 'object') Object.assign(this, props); }
    heading1: string = 'heading';
    heading2: string = 'title';
    heading3: string = 'titleSmall';
    heading4: string = 'label';
    body: string = 'bodyM';
    blockGap: number = 14;
    codeLineNumbers: boolean = false;
    codeInverse: boolean = false;
}

