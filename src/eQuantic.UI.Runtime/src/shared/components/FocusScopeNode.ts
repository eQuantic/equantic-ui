import { $TraversalEdgeBehaviorValue } from "../runtime-exports";
import { FocusNode } from "./FocusNode";

export class FocusScopeNode extends FocusNode {
    constructor() {
        const $$traversalEdgeBehavior: $TraversalEdgeBehaviorValue = 'closedLoop';
        super();
        this.focusedChild = null;
        this.traversalEdgeBehavior = $$traversalEdgeBehavior;
    }

    focusedChild!: FocusNode | null;
    traversalEdgeBehavior!: $TraversalEdgeBehaviorValue;

    toString(): string {
        return 'eQuantic.UI.Primitives.FocusScopeNode';
    }
}

