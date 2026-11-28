import { $FocusRequestKindValue, FocusNode } from "../runtime-exports";

export class FocusRequest {
    constructor(kind: $FocusRequestKindValue, node: FocusNode, ticket: number) {
        this.kind = 'focus';
        this.node = null!;
        this.ticket = 0;
        this.kind = kind;
        this.node = node;
        this.ticket = ticket;
    }

    kind!: $FocusRequestKindValue;
    node!: FocusNode;
    ticket!: number;

    toString(): string {
        return 'eQuantic.UI.Primitives.FocusRequest';
    }
}

