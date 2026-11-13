// A component IS a node of the vocabulary, as in C# (`UiComponent : VisualNode`) and in Flutter (a
// `StatelessWidget` is a `Widget`). The two classes cannot share a base here, because the
// vocabulary's `VisualNode` lowers itself through `shared/lowering` and `core/` imports nothing of
// `shared/` at run time; so `UiComponent` declares the node's members itself, and this file is what
// keeps them the same. `tsc` reads it with the rest of `src/` and nothing imports it, so it ships
// nothing: a member the vocabulary's `VisualNode` gains that `UiComponent` lacks, or a kind that is
// not a `NodeKind`, fails the type check here.

import type { UiComponent } from './component';
import type { VisualNode } from '../shared/vocabulary';
import type { NodeKind } from '../shared/node-kinds.generated';

export const aComponentIsANode = (component: UiComponent): VisualNode => component;

export const aComponentsKindIsANodeKind = (component: UiComponent): NodeKind => component.nodeKind;
