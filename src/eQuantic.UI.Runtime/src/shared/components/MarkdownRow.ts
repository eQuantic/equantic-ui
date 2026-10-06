import { MarkdownCell } from "../runtime-exports";

export class MarkdownRow {
    constructor() {
        this.cells = [];
    }

    declare cells: MarkdownCell[];
}

