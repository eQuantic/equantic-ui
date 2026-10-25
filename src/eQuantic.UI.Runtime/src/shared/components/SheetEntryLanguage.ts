import { $eq, CodeLanguageRules, CodeToken } from "../runtime-exports";

export class SheetEntryLanguage {
    constructor() {}

    get name(): string {
        return 'Cell';
    }

    get rules(): CodeLanguageRules {
        return SheetEntryLanguage.nothingPaired;
    }

    static $slots: any = null;
    static $failure: any = null;

    static $init(): any {
        if (SheetEntryLanguage.$slots === null) {
            if (SheetEntryLanguage.$failure !== null) throw SheetEntryLanguage.$failure;
            let $slots: any = SheetEntryLanguage.$slots = {};
            try {
                $slots.nothingPaired = null;
                $slots.nothingPaired = (() => {
        const $o = new CodeLanguageRules();
        $o.brackets = [];
        $o.quotes = [];
        $o.indentAfter = [];
        $o.outdentOn = [];
        $o.insertSpaces = false;
        return $o;
    })();
            } catch ($error) {
                SheetEntryLanguage.$slots = null;
                throw SheetEntryLanguage.$failure = $eq.exceptions.typeInitialization('eQuantic.UI.Sheet.SheetEntryLanguage', $error);
            }
        }
        return SheetEntryLanguage.$slots;
    }

    static get nothingPaired(): CodeLanguageRules {
        return SheetEntryLanguage.$init().nothingPaired;
    }

    static set nothingPaired(value: CodeLanguageRules) {
        SheetEntryLanguage.$init().nothingPaired = value;
    }

    tokenize(line: string, _state: number, into: CodeToken[]) {
        if (line.length > 0) into.push(new CodeToken(0, line.length, 'plain'));
        return 0;
    }

    get keywords(): string[] {
        return [];
    }

    toString(): string {
        return 'eQuantic.UI.Sheet.SheetEntryLanguage';
    }
}

