import { $eq } from "@equantic/runtime";
import { EmittedShapeBase } from "./EmittedShapeBase";

export class EmittedTagged<T extends EmittedShapeBase> { declare shape: T; declare tag: string; constructor(shape: any = null, tag: any = null) {this.shape = shape;this.tag = tag;} equals(o: unknown) { return o instanceof EmittedTagged && o.constructor === this.constructor && $eq.sameClosure(this, o) && $eq.collections.sameGeneric(this.shape, o.shape) && $eq.equals(this.tag, o.tag); } with(patch: any): EmittedTagged<T> { return $eq.withPatch(this, patch); } static $record = true; getHashCode(): number { return $eq.hash.combine(this.shape, this.tag); } get area() {return this.shape.area;} toString() { return $eq.text.record(this, 'EmittedTagged', ['Shape', 'Tag', ['Area', 'int32']]); } }
