export abstract class EmittedStep {
    constructor() {}

    run() {
        return ((this.draw() + this.measure() | 0) + this.layout() | 0) + this.paint() | 0;
    }

    abstract draw(): number;
    abstract measure(): number;
    abstract layout(): number;
    abstract paint(): number;
    abstract loadAsync(from: number, count?: number): Promise<number>;
    abstract saveAsync(...values: number[]): Promise<void>;
    abstract pick<T>(first: T, second: T): T;

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedStep';
    }
}

