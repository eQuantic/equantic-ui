import { MarkdownRun } from "../runtime-exports";

export class MarkdownListItem {
    constructor() {
        this.runs = [];
        this.depth = 0;
        this.marker = '•';
    }

    declare runs: MarkdownRun[];
    declare depth: number;
    declare marker: string;
}

