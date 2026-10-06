import { MarkdownCell, MarkdownListItem, MarkdownRow, MarkdownRun } from "../runtime-exports";

export class MarkdownBlock {
    constructor() {
        this.kind = 'paragraph';
        this.level = 0;
        this.text = '';
        this.id = '';
        this.runs = [];
        this.items = [];
        this.lang = '';
        this.raw = '';
        this.head = [];
        this.rows = [];
    }

    kind!: string;
    level!: number;
    text!: string;
    id!: string;
    runs!: MarkdownRun[];
    items!: MarkdownListItem[];
    lang!: string;
    raw!: string;
    head!: MarkdownCell[];
    rows!: MarkdownRow[];
}

