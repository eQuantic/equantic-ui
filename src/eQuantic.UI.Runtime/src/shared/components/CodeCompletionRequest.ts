import { $eq, CancellationTokenSource } from "../runtime-exports";

export class CodeCompletionRequest {
    constructor(provider: any, word: string) {
        this.provider = null;
        this.word = null!;
        this.cancellation = $eq.cancellation.source();
        this.answered = false;
        this.provider = provider;
        this.word = word;
    }

    provider!: any;
    word!: string;
    cancellation!: CancellationTokenSource;
    answered!: boolean;

    toString(): string {
        return 'eQuantic.UI.Code.CodeCompletionRequest';
    }
}

