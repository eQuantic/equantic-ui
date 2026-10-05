import { CodeCompletionList, CodeCompletionOffer, CodePosition } from "../runtime-exports";

export class CodeCompletionAnswer {
    constructor(provider: any, askedAt: CodePosition, list: CodeCompletionList, props?: any) {
        this.provider = provider;
        this.askedAt = askedAt;
        this.isIncomplete = list.isIncomplete;
        for (const item of list.items) this.offers.push(new CodeCompletionOffer(this, item));
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    declare provider: any;
    askedAt: CodePosition = new CodePosition();
    isIncomplete: boolean = false;
    offers: CodeCompletionOffer[] = [];
}

