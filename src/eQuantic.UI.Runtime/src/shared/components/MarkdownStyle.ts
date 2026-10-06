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

    declare heading1: TypeRoleValue;
    declare heading2: TypeRoleValue;
    declare heading3: TypeRoleValue;
    declare heading4: TypeRoleValue;
    declare body: TypeRoleValue;
    declare blockGap: number;
    declare codeLineNumbers: boolean;
    declare codeInverse: boolean;
}

