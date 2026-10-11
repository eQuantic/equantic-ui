import { MarkdownRun } from "../runtime-exports";

export class MarkdownCell {
    constructor() {
        this.runs = [];
    }

    runs!: MarkdownRun[];

    toString(): string {
        return 'eQuantic.UI.Components.MarkdownCell';
    }
}

