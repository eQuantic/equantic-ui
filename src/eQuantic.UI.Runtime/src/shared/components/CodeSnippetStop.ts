import { $eq, CodeRange } from "../runtime-exports";

export class CodeSnippetStop {
    constructor(index: number, final: boolean) {
        this.index = 0;
        this.final = false;
        this.choices = null;
        this.ranges = [];
        this.enclosing = [];
        this.hasPrimary = false;
        this.dropped = false;
        this.index = index;
        this.final = final;
    }

    index!: number;
    final!: boolean;
    choices!: string[] | null;
    ranges!: CodeRange[];
    enclosing!: number[][];
    hasPrimary!: boolean;
    dropped!: boolean;

    add(range: CodeRange, enclosing: number[], first: boolean) {
        if (first) {
            $eq.collections.listInsert(this.ranges, 0, range);
            $eq.collections.listInsert(this.enclosing, 0, enclosing);
            this.hasPrimary = true;
            return;
        }
        this.ranges.push(range);
        this.enclosing.push(enclosing);
    }

    removeAt(index: number) {
        $eq.collections.listRemoveAt(this.ranges, index);
        $eq.collections.listRemoveAt(this.enclosing, index);
    }

    nestedIn(index: number) {
        for (const enclosing of this.enclosing) if (enclosing.includes(index)) return true;
        return false;
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeSnippetStop';
    }
}

