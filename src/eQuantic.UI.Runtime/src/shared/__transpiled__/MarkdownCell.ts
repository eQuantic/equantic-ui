import { MarkdownRun } from "@equantic/runtime";
export class MarkdownCell {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    runs: MarkdownRun[] = [];
}

