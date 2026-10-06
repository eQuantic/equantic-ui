import { CodeCompletionAnswer, CodeCompletionItem } from "../runtime-exports";

export class CodeCompletionOffer {
    constructor(answer: CodeCompletionAnswer, item: CodeCompletionItem) {
        this.answer = null!;
        this.item = null!;
        this.resolving = false;
        this.group = 0;
        this.sortKey = '';
        this.answer = answer;
        this.item = item;
    }

    answer!: CodeCompletionAnswer;
    item!: CodeCompletionItem;
    resolving!: boolean;
    group!: number;
    sortKey!: string;
}

