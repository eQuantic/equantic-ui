import { $eq, CodePatchLineKindValue } from "../runtime-exports";

export class CodePatchLine { declare kind: CodePatchLineKindValue; declare text: string; constructor(kind: any = 'context', text: any = null) { this.kind = kind; this.text = text; } equals(o: unknown) { return o instanceof CodePatchLine && $eq.equals(this.kind, o.kind) && $eq.equals(this.text, o.text); } with(patch: any) { return new CodePatchLine(('kind' in patch ? patch.kind : this.kind), ('text' in patch ? patch.text : this.text)); } toString() { return `CodePatchLine { Kind = ${this.kind}, Text = ${this.text} }`; } }
