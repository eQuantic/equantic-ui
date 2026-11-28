import { $FocusRequestKindValue, FocusManager, FocusRequest, FocusScopeNode } from "../runtime-exports";

export class FocusNode {
    constructor() {
        this.changed = null;
        this.canRequestFocus = true;
        this.skipTraversal = false;
        this.hasFocus = false;
        this.hasPrimaryFocus = false;
        this.manager = null;
        this.parent = null;
        this.children = [];
        this.enclosingScope = null;
        this.frame = 0;
        this.pendingAsk = 0;
        this.onFocusChange = null;
    }

    changed!: (() => void) | null;
    canRequestFocus!: boolean;
    skipTraversal!: boolean;
    hasFocus!: boolean;
    hasPrimaryFocus!: boolean;
    manager!: FocusManager | null;
    parent!: FocusNode | null;
    children!: FocusNode[];
    enclosingScope!: FocusScopeNode | null;
    frame!: number;
    pendingAsk!: number;
    onFocusChange!: ((bool: boolean) => void) | null;
    static _asks: number = 0;

    requestFocus() {
        return this.ask('focus');
    }

    unfocus() {
        if (this.hasFocus) this.ask('unfocus');
    }

    nextFocus() {
        let manager: any; 
        return (manager = this.manager) != null && manager.askToMove(this, true);
    }

    previousFocus() {
        let manager: any; 
        return (manager = this.manager) != null && manager.askToMove(this, false);
    }

    raiseChanged() {
        return this.changed?.();
    }

    static nextAsk() {
        FocusNode._asks = FocusNode._asks + 1 | 0;
        return FocusNode._asks;
    }

    ask(kind: $FocusRequestKindValue) {
        let ask = FocusNode.nextAsk();
        let manager: any; 
        if ((manager = this.manager) != null) manager.ask(new FocusRequest(kind, this, ask)); else this.pendingAsk = ask;
    }

    toString(): string {
        return 'eQuantic.UI.Primitives.FocusNode';
    }
}

