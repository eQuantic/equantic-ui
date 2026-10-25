import { $eq, CodeCompletionList, CodeCompletionOffer, CodeEdit, CodeRange } from "../runtime-exports";

export class CodeCompletionAnswer {
    constructor(provider: any, askedWord: string, asked: number, list: CodeCompletionList) {
        this._from = CodeRange.$zero();
        this._to = CodeRange.$zero();
        this._through = -1;
        this.provider = null;
        this.askedWord = null!;
        this.asked = 0;
        this.isIncomplete = false;
        this.offers = [];
        this.provider = provider;
        this.askedWord = askedWord;
        this.asked = asked;
        this.isIncomplete = list.isIncomplete;
        for (const item of list.items) this.offers.push(new CodeCompletionOffer(this, item));
    }

    _from!: CodeRange;
    _to!: CodeRange;
    _through!: number;
    provider!: any;
    askedWord!: string;
    asked!: number;
    isIncomplete!: boolean;
    offers!: CodeCompletionOffer[];

    carried(range: CodeRange, edits: CodeEdit[]) {
        if (this._through < 0 || !$eq.equals(range, this._from)) {
            this._from = range;
            this._to = range;
            this._through = this.asked;
        }
        for (; this._through < $eq.collections.count(edits); this._through++) this._to = new CodeRange($eq.collections.item(edits, this._through).carryAnchor(this._to.start), $eq.collections.item(edits, this._through).carryAnchor(this._to.end));
        return this._to;
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeCompletionAnswer';
    }
}

