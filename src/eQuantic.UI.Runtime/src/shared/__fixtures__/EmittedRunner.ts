export class EmittedRunner {
    constructor(step: (value: number) => number = undefined!, live: boolean = undefined!) {
        if (arguments.length === 1) {
            let $c0: any, $c1: any;
            {
                let step: any = arguments[0];
                $c0 = step;
                $c1 = true;
            }
            step = $c0;
            live = $c1;
        }
        this._step = null!;
        this.live = false;
        this._step = step;
        this.live = live;
    }

    _step!: (value: number) => number;
    live!: boolean;

    run(value: number) {
        return this._step(value);
    }

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedRunner';
    }
}

