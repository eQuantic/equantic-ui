import { CodeRange } from "../runtime-exports";

export class CodeSnippetStop {
    constructor(index: number, parent: number, final: boolean) {
        this.index = 0;
        this.parent = 0;
        this.final = false;
        this.choices = null;
        this.ranges = [];
        this.hasPrimary = false;
        this.dropped = false;
        this.index = index;
        this.parent = parent;
        this.final = final;
    }

    index!: number;
    parent!: number;
    final!: boolean;
    choices!: string[] | null;
    ranges!: CodeRange[];
    hasPrimary!: boolean;
    dropped!: boolean;

    toString(): string {
        return 'eQuantic.UI.Code.CodeSnippetStop';
    }
}

