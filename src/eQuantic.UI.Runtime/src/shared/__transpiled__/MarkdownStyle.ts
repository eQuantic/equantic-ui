import { TypeRoleValue } from "@equantic/runtime";
export class MarkdownStyle {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    heading1: TypeRoleValue = 'heading';
    heading2: TypeRoleValue = 'title';
    heading3: TypeRoleValue = 'titleSmall';
    heading4: TypeRoleValue = 'label';
    body: TypeRoleValue = 'bodyM';
    blockGap: number = 14;
    codeLineNumbers: boolean = false;
    codeInverse: boolean = false;
}

