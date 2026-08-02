import { $eq } from "../runtime-exports";

export class AccordionItem { declare title: string; declare content: any; constructor(title: any = null, content: any = null) { this.title = title; this.content = content; } equals(o: unknown) { return o instanceof AccordionItem && $eq.equals(this.title, o.title) && $eq.equals(this.content, o.content); } with(patch: any) { return new AccordionItem(('title' in patch ? patch.title : this.title), ('content' in patch ? patch.content : this.content)); } toString() { return `AccordionItem { Title = ${this.title}, Content = ${this.content} }`; } }
