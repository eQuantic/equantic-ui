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

    declare kind: string;
    declare level: number;
    declare text: string;
    declare id: string;
    declare runs: MarkdownRun[];
    declare items: MarkdownListItem[];
    declare lang: string;
    declare raw: string;
    declare head: MarkdownCell[];
    declare rows: MarkdownRow[];
}

