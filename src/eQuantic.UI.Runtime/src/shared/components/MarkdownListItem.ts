import { MarkdownRun } from "../runtime-exports";

export class MarkdownListItem {
    constructor() {
        this.runs = [];
        this.depth = 0;
        this.marker = '•';
    }

    runs!: MarkdownRun[];
    depth!: number;
    marker!: string;
}

