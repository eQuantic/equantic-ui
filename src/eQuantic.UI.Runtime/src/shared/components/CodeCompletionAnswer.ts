import { CodeCompletionList, CodeCompletionOffer } from "../runtime-exports";

export class CodeCompletionAnswer {
    constructor(provider: any, askedWord: string, askedLineLength: number, list: CodeCompletionList) {
        this.provider = null;
        this.askedWord = null!;
        this.askedLineLength = 0;
        this.isIncomplete = false;
        this.offers = [];
        this.provider = provider;
        this.askedWord = askedWord;
        this.askedLineLength = askedLineLength;
        this.isIncomplete = list.isIncomplete;
        for (const item of list.items) this.offers.push(new CodeCompletionOffer(this, item));
    }

    provider!: any;
    askedWord!: string;
    askedLineLength!: number;
    isIncomplete!: boolean;
    offers!: CodeCompletionOffer[];

    toString(): string {
        return 'eQuantic.UI.Code.CodeCompletionAnswer';
    }
}

