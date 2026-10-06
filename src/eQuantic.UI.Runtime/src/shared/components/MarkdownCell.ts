import { MarkdownRun } from "../runtime-exports";

export class MarkdownCell {
    constructor() {
        this.runs = [];
    }

    declare runs: MarkdownRun[];
}

