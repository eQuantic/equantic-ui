import { MarkdownCell } from "../runtime-exports";
export class MarkdownRow {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }
    cells: MarkdownCell[] = [];
}

