import { $eq, CodePatchLineKindValue } from "../runtime-exports";

export class CodePatchLine { declare kind: CodePatchLineKindValue; declare text: string; constructor(kind: any = 'context', text: any = null) { this.kind = kind; this.text = text; } equals(o: unknown) { return o instanceof CodePatchLine && o.constructor === this.constructor && $eq.equals(this.kind, o.kind) && $eq.equals(this.text, o.text); } with(patch: any): CodePatchLine { return $eq.withPatch(this, patch); } toString() { return `CodePatchLine { Kind = ${this.kind}, Text = ${this.text} }`; } }
