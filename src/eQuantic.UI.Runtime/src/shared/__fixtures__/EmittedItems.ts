import { $eq } from "@equantic/runtime";
import { EmittedItem } from "./EmittedItem";

export class EmittedItems {
    static $slots: any = null;
    static $failure: any = null;

    static $init(): any {
        if (EmittedItems.$slots === null) {
            if (EmittedItems.$failure !== null) throw EmittedItems.$failure;
            let $slots: any = EmittedItems.$slots = {};
            try {
                $slots.byDepth = null;
                $slots.byDepth = ((a, b) => Math.sign(a.depth - b.depth)) satisfies (x: any, y: any) => number;
            } catch ($error) {
                EmittedItems.$slots = null;
                throw EmittedItems.$failure = $eq.exceptions.typeInitialization('eQuantic.UI.Web.Tests.Fixtures.EmittedItems', $error);
            }
        }
        return EmittedItems.$slots;
    }

    static get byDepth(): (x: any, y: any) => number {
        return EmittedItems.$init().byDepth;
    }

    static set byDepth(value: (x: any, y: any) => number) {
        EmittedItems.$init().byDepth = value;
    }

    static order(items: EmittedItem[]) {
        return $eq.collections.listSortBy(items, EmittedItems.byDepth, 'System.Comparison`1[eQuantic.UI.Web.Tests.Fixtures.EmittedItem]');
    }
}

