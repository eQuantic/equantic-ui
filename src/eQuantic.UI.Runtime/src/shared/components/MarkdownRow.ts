import { MarkdownCell } from "../runtime-exports";

export class MarkdownRow {
    constructor() {
        this.cells = [];
    }

    cells!: MarkdownCell[];

    toString(): string {
        return 'eQuantic.UI.Components.MarkdownRow';
    }
}

