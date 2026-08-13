import { $eq } from "../runtime-exports";

export class FieldRule { declare message: string; declare holds: (value: string) => boolean; constructor(message: any = null, holds: any = null) { this.message = message; this.holds = holds; } equals(o: unknown) { return o instanceof FieldRule && $eq.equals(this.message, o.message) && $eq.equals(this.holds, o.holds); } with(patch: any) { return new FieldRule(('message' in patch ? patch.message : this.message), ('holds' in patch ? patch.holds : this.holds)); } toString() { return `FieldRule { Message = ${this.message}, Holds = ${this.holds} }`; } }
