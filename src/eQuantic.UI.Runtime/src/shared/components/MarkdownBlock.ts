import { MarkdownCell, MarkdownListItem, MarkdownRow, MarkdownRun } from "../runtime-exports";

export class MarkdownBlock {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    kind: string = 'paragraph';
    level: number = 0;
    text: string = '';
    id: string = '';
    runs: MarkdownRun[] = [];
    items: MarkdownListItem[] = [];
    lang: string = '';
    raw: string = '';
    head: MarkdownCell[] = [];
    rows: MarkdownRow[] = [];
}

