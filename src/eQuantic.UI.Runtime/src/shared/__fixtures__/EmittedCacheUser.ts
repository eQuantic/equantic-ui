import { $eq } from "@equantic/runtime";
import { EmittedCache } from "./EmittedCache";

export class EmittedCacheUser {
    constructor() {}

    use() {
        $eq.statics(EmittedCache, 'int').keep(3);
        $eq.statics(EmittedCache, 'string').keep('a');
        let keep = (($0) => $eq.delegates.bound($0, $0.keep))($eq.statics(EmittedCache, 'int'));
        return (keep($eq.statics(EmittedCache, 'int').hits) + $eq.statics(EmittedCache, 'string').count | 0) + 4 | 0;
    }

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedCacheUser';
    }
}

