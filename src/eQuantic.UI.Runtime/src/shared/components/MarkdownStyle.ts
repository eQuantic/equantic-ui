import { TypeRoleValue } from "../runtime-exports";

export class MarkdownStyle {
    constructor() {
        this.heading1 = 'heading';
        this.heading2 = 'title';
        this.heading3 = 'titleSmall';
        this.heading4 = 'label';
        this.body = 'bodyM';
        this.blockGap = 14;
        this.codeLineNumbers = false;
        this.codeInverse = false;
    }

    heading1!: TypeRoleValue;
    heading2!: TypeRoleValue;
    heading3!: TypeRoleValue;
    heading4!: TypeRoleValue;
    body!: TypeRoleValue;
    blockGap!: number;
    codeLineNumbers!: boolean;
    codeInverse!: boolean;

    toString(): string {
        return 'eQuantic.UI.Components.MarkdownStyle';
    }
}

