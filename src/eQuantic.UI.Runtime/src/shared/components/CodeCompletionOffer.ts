import { CodeCompletionAnswer, CodeCompletionItem } from "../runtime-exports";

export class CodeCompletionOffer {
    constructor(answer: CodeCompletionAnswer, item: CodeCompletionItem, props?: any) {
        this.answer = answer;
        this.item = item;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    declare answer: CodeCompletionAnswer;
    declare item: CodeCompletionItem;
    resolving: boolean = false;
}

