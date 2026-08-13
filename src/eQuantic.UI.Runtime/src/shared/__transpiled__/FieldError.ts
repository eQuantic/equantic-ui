import { $eq } from "@equantic/runtime";

export class FieldError { declare field: string; declare message: string; constructor(field: any = null, message: any = null) { this.field = field; this.message = message; } equals(o: unknown) { return o instanceof FieldError && $eq.equals(this.field, o.field) && $eq.equals(this.message, o.message); } with(patch: any) { return new FieldError(('field' in patch ? patch.field : this.field), ('message' in patch ? patch.message : this.message)); } toString() { return `FieldError { Field = ${this.field}, Message = ${this.message} }`; } }
