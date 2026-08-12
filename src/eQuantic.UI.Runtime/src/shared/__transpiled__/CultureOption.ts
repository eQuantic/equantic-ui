import { $eq } from "@equantic/runtime";

export class CultureOption { declare name: string; declare label: string; constructor(name: any = null, label: any = null) { this.name = name; this.label = label; } equals(o: unknown) { return o instanceof CultureOption && $eq.equals(this.name, o.name) && $eq.equals(this.label, o.label); } with(patch: any) { return new CultureOption(('name' in patch ? patch.name : this.name), ('label' in patch ? patch.label : this.label)); } toString() { return `CultureOption { Name = ${this.name}, Label = ${this.label} }`; } }
