import { CodeCompletionList, CodeCompletionOffer } from "../runtime-exports";

export class CodeCompletionAnswer {
    constructor(provider: any, askedLineLength: number, list: CodeCompletionList, props?: any) {
        this.provider = provider;
        this.askedLineLength = askedLineLength;
        this.isIncomplete = list.isIncomplete;
        for (const item of list.items) this.offers.push(new CodeCompletionOffer(this, item));
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    declare provider: any;
    askedLineLength: number = 0;
    isIncomplete: boolean = false;
    offers: CodeCompletionOffer[] = [];
}

