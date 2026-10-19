import { $eq, CancellationTokenSource } from "../runtime-exports";

export class CodeCompletionRequest {
    constructor(provider: any) {
        this.provider = null;
        this.cancellation = $eq.cancellation.source();
        this.answered = false;
        this.provider = provider;
    }

    provider!: any;
    cancellation!: CancellationTokenSource;
    answered!: boolean;

    toString(): string {
        return 'eQuantic.UI.Code.CodeCompletionRequest';
    }
}

