import { $eq } from "@equantic/runtime";

export class EmittedCache {
    static size: number = 4;

    static get count(): number {
        this.$init();
        return this._all.length;
    }

    static $slots: any = null;
    static $failure: any = null;

    static $init(): any {
        if (this.$slots === null) {
            if (this.$failure !== null) throw this.$failure;
            let $slots: any = this.$slots = {};
            try {
                $slots.last = null;
                $slots.hits = 0;
                $slots._all = null;
                $slots._all = [];
                (() => {
                    this.hits = 0;
                })();
            } catch ($error) {
                this.$slots = null;
                throw this.$failure = $eq.exceptions.typeInitialization('eQuantic.UI.Web.Tests.Fixtures.EmittedCache`1', $error);
            }
        }
        return this.$slots;
    }

    static get last(): T | null {
        return this.$init().last;
    }

    static set last(value: T | null) {
        this.$init().last = value;
    }

    static get hits(): number {
        return this.$init().hits;
    }

    static set hits(value: number) {
        this.$init().hits = value;
    }

    static get _all(): T[] {
        return this.$init()._all;
    }

    static set _all(value: T[]) {
        this.$init()._all = value;
    }

    static keep(value: T) {
        this.$init();
        this.last = value;
        $eq.collections.listAdd(this._all, value);
        this.hits = this.hits + 1 | 0;
        let echo = $eq.delegates.bound(this, this.echo);
        return echo(value);
    }

    static echo(value: T) {
        this.$init();
        const local = (v: T) => {
            return v;
        };
        return local(value);
    }
}

type T = any;
