import { CodeCompletionList, CodeCompletionOffer } from "../runtime-exports";

export class CodeCompletionAnswer {
    constructor(provider: any, askedLineLength: number, list: CodeCompletionList) {
        this.provider = null;
        this.askedLineLength = 0;
        this.isIncomplete = false;
        this.offers = [];
        this.provider = provider;
        this.askedLineLength = askedLineLength;
        this.isIncomplete = list.isIncomplete;
        for (const item of list.items) this.offers.push(new CodeCompletionOffer(this, item));
    }

    provider!: any;
    askedLineLength!: number;
    isIncomplete!: boolean;
    offers!: CodeCompletionOffer[];
}

