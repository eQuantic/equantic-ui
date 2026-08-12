import { MarkdownCell } from "@equantic/runtime";
export class MarkdownRow {
    constructor(props?: any) {  if (props && typeof props === 'object') Object.assign(this, props); }
    cells: MarkdownCell[] = [];
}

