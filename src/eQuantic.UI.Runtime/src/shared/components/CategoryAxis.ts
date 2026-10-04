import { $eq } from "../runtime-exports";

export class CategoryAxis { declare categories: string[]; declare title: string | null; constructor(categories: any = null, title: any = null) { this.categories = categories; this.title = title; } equals(o: unknown) { return o instanceof CategoryAxis && o.constructor === this.constructor && $eq.equals(this.categories, o.categories) && $eq.equals(this.title, o.title); } with(patch: any): CategoryAxis { return $eq.withPatch(this, patch); } toString() { return `CategoryAxis { Categories = ${this.categories}, Title = ${this.title} }`; } }
