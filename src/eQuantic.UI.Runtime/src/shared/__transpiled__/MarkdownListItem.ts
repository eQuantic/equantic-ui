import { MarkdownRun } from "@equantic/runtime";
export class MarkdownListItem {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    runs: MarkdownRun[] = [];
    depth: number = 0;
    marker: string = '•';
}

