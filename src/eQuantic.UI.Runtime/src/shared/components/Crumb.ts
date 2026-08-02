import { $eq } from "../runtime-exports";

export class Crumb { declare label: string | null; declare href: string | null; constructor(label: any = null, href: any = null) { this.label = label; this.href = href; } equals(o: unknown) { return o instanceof Crumb && $eq.equals(this.label, o.label) && $eq.equals(this.href, o.href); } with(patch: any) { return new Crumb(('label' in patch ? patch.label : this.label), ('href' in patch ? patch.href : this.href)); } toString() { return `Crumb { Label = ${this.label}, Href = ${this.href} }`; } }
