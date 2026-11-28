import { FocusNode } from "../runtime-exports";

export class FocusEvent {
    constructor(node: FocusNode, hasFocus: boolean, focusChanged: boolean) {
        this.node = null!;
        this.hasFocus = false;
        this.focusChanged = false;
        this.node = node;
        this.hasFocus = hasFocus;
        this.focusChanged = focusChanged;
    }

    node!: FocusNode;
    hasFocus!: boolean;
    focusChanged!: boolean;

    toString(): string {
        return 'eQuantic.UI.Primitives.FocusEvent';
    }
}

