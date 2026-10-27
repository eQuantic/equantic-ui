import { $eq, CalendarNames } from "../runtime-exports";

export class SdkStrings {
    static get dismiss(): string {
        return $eq.str('SdkResources', 'Dismiss');
    }

    static get cookieConsentTitle(): string {
        return $eq.str('SdkResources', 'CookieConsentTitle');
    }

    static get cookieConsentBody(): string {
        return $eq.str('SdkResources', 'CookieConsentBody');
    }

    static get acceptCookies(): string {
        return $eq.str('SdkResources', 'AcceptCookies');
    }

    static get rejectCookies(): string {
        return $eq.str('SdkResources', 'RejectCookies');
    }

    static get privacyPolicy(): string {
        return $eq.str('SdkResources', 'PrivacyPolicy');
    }

    static get remove(): string {
        return $eq.str('SdkResources', 'Remove');
    }

    static get searchPlaceholder(): string {
        return $eq.str('SdkResources', 'SearchPlaceholder');
    }

    static get clearSearch(): string {
        return $eq.str('SdkResources', 'ClearSearch');
    }

    static get codeEditor(): string {
        return $eq.str('SdkResources', 'CodeEditor');
    }

    static get previousChange(): string {
        return $eq.str('SdkResources', 'PreviousChange');
    }

    static get nextChange(): string {
        return $eq.str('SdkResources', 'NextChange');
    }

    static get showInline(): string {
        return $eq.str('SdkResources', 'ShowInline');
    }

    static get showSideBySide(): string {
        return $eq.str('SdkResources', 'ShowSideBySide');
    }

    static get diffOriginal(): string {
        return $eq.str('SdkResources', 'DiffOriginal');
    }

    static get diffModified(): string {
        return $eq.str('SdkResources', 'DiffModified');
    }

    static get find(): string {
        return $eq.str('SdkResources', 'Find');
    }

    static get previousMatch(): string {
        return $eq.str('SdkResources', 'PreviousMatch');
    }

    static get nextMatch(): string {
        return $eq.str('SdkResources', 'NextMatch');
    }

    static get closeFind(): string {
        return $eq.str('SdkResources', 'CloseFind');
    }

    static get goTo(): string {
        return $eq.str('SdkResources', 'GoTo');
    }

    static get goToStart(): string {
        return $eq.str('SdkResources', 'GoToStart');
    }

    static get noSymbols(): string {
        return $eq.str('SdkResources', 'NoSymbols');
    }

    static get noMatchingSymbols(): string {
        return $eq.str('SdkResources', 'NoMatchingSymbols');
    }

    static get noSymbolProvider(): string {
        return $eq.str('SdkResources', 'NoSymbolProvider');
    }

    static get readingSymbols(): string {
        return $eq.str('SdkResources', 'ReadingSymbols');
    }

    static get filterPlaces(): string {
        return $eq.str('SdkResources', 'FilterPlaces');
    }

    static get noMatchingPlaces(): string {
        return $eq.str('SdkResources', 'NoMatchingPlaces');
    }

    static get noDefinition(): string {
        return $eq.str('SdkResources', 'NoDefinition');
    }

    static get noDeclaration(): string {
        return $eq.str('SdkResources', 'NoDeclaration');
    }

    static get noTypeDefinition(): string {
        return $eq.str('SdkResources', 'NoTypeDefinition');
    }

    static get noImplementation(): string {
        return $eq.str('SdkResources', 'NoImplementation');
    }

    static get noReferences(): string {
        return $eq.str('SdkResources', 'NoReferences');
    }

    static get copyCode(): string {
        return $eq.str('SdkResources', 'CopyCode');
    }

    static get previousMonth(): string {
        return $eq.str('SdkResources', 'PreviousMonth');
    }

    static get nextMonth(): string {
        return $eq.str('SdkResources', 'NextMonth');
    }

    static get today(): string {
        return $eq.str('SdkResources', 'Today');
    }

    static get chooseDate(): string {
        return $eq.str('SdkResources', 'ChooseDate');
    }

    static get chooseTime(): string {
        return $eq.str('SdkResources', 'ChooseTime');
    }

    static get dateFormatHint(): string {
        return SdkStrings.hint(CalendarNames.shortDatePattern, SdkStrings.dateFormatLetters);
    }

    static get dateFormatLetters(): string {
        return $eq.str('SdkResources', 'DateFormatLetters');
    }

    static get spreadsheet(): string {
        return $eq.str('SdkResources', 'Spreadsheet');
    }

    static get formulaBar(): string {
        return $eq.str('SdkResources', 'FormulaBar');
    }

    static get nameBox(): string {
        return $eq.str('SdkResources', 'NameBox');
    }

    static get addToSelection(): string {
        return $eq.str('SdkResources', 'AddToSelection');
    }

    static get back(): string {
        return $eq.str('SdkResources', 'Back');
    }

    static get nothingSelected(): string {
        return $eq.str('SdkResources', 'NothingSelected');
    }

    static get showAsTable(): string {
        return $eq.str('SdkResources', 'ShowAsTable');
    }

    static get showAsChart(): string {
        return $eq.str('SdkResources', 'ShowAsChart');
    }

    static lineNumbered(number: number) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'LineNumbered'), $eq.text.asNumber(number, 'int32'));
    }

    static unchangedLines(count: number) {
        return count === 1 ? $eq.str('SdkResources', 'UnchangedLine') : $eq.text.stringFormat($eq.str('SdkResources', 'UnchangedLines'), $eq.text.asNumber(count, 'int32'));
    }

    static hiddenLines(count: number) {
        return count === 1 ? $eq.str('SdkResources', 'HiddenLine') : $eq.text.stringFormat($eq.str('SdkResources', 'HiddenLines'), $eq.text.asNumber(count, 'int32'));
    }

    static goToLinePrompt(lines: number) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'GoToLinePrompt'), $eq.text.asNumber(lines, 'int32'));
    }

    static goToLineOrColumn(line: number) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'GoToLineOrColumn'), $eq.text.asNumber(line, 'int32'));
    }

    static goToColumnPrompt(line: number, columns: number) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'GoToColumnPrompt'), $eq.text.asNumber(line, 'int32'), $eq.text.asNumber(columns, 'int32'));
    }

    static goToPosition(line: number, column: number) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'GoToPosition'), $eq.text.asNumber(line, 'int32'), $eq.text.asNumber(column, 'int32'));
    }

    static symbolsCount(count: number) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'SymbolsCount'), $eq.text.asNumber(count, 'int32'));
    }

    static definitionsCount(count: number) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'DefinitionsCount'), $eq.text.asNumber(count, 'int32'));
    }

    static declarationsCount(count: number) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'DeclarationsCount'), $eq.text.asNumber(count, 'int32'));
    }

    static typeDefinitionsCount(count: number) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'TypeDefinitionsCount'), $eq.text.asNumber(count, 'int32'));
    }

    static implementationsCount(count: number) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'ImplementationsCount'), $eq.text.asNumber(count, 'int32'));
    }

    static referencesCount(count: number) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'ReferencesCount'), $eq.text.asNumber(count, 'int32'));
    }

    static noDefinitionFound(word: string) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'NoDefinitionFound'), word);
    }

    static noDeclarationFound(word: string) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'NoDeclarationFound'), word);
    }

    static noTypeDefinitionFound(word: string) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'NoTypeDefinitionFound'), word);
    }

    static noImplementationFound(word: string) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'NoImplementationFound'), word);
    }

    static noReferencesFound(word: string) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'NoReferencesFound'), word);
    }

    static hint(pattern: string, letters: string) {
        let day = letters.length > 0 ? letters[0] : 'D';
        let month = letters.length > 1 ? letters[1] : 'M';
        let year = letters.length > 2 ? letters[2] : 'Y';
        let hint = $eq.text.stringBuilder();
        let i = 0;
        while (i < pattern.length) {
            let letter = pattern[i];
            if (letter === '\'') {
                let close = $eq.text.indexOfChar(pattern, '\'', i + 1);
                if (close < 0) {
                    hint.append($eq.text.substring(pattern, i + 1));
                    break;
                }
                hint.append($eq.text.substring(pattern, i + 1, close - i - 1));
                i = close + 1;
                continue;
            }
            let run = 1;
            while (i + run < pattern.length && pattern[i + run] === letter) run++;
            if (letter === 'd') hint.append(day).append(day); else if (letter === 'M') hint.append(month).append(month); else if (letter === 'y') hint.append(year).append(year).append(year).append(year); else hint.append($eq.text.substring(pattern, i, run));
            i += run;
        }
        return hint.toString();
    }

    static rangeSize(rows: number, columns: number) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'RangeSize'), $eq.text.asNumber(rows, 'int32'), $eq.text.asNumber(columns, 'int32'));
    }

    static cellAnnouncement(address: string, text: string) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'CellAnnouncement'), address, text.length === 0 ? $eq.str('SdkResources', 'CellBlank') : text);
    }

    static rangeAnnouncement(address: string, rows: number, columns: number) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'RangeAnnouncement'), address, rows === 1 ? $eq.str('SdkResources', 'RangeOneRow') : $eq.text.stringFormat($eq.str('SdkResources', 'RangeRows'), $eq.text.asNumber(rows, 'int32')), columns === 1 ? $eq.str('SdkResources', 'RangeOneColumn') : $eq.text.stringFormat($eq.str('SdkResources', 'RangeColumns'), $eq.text.asNumber(columns, 'int32')));
    }

    static sum(value: string) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'StatusSum'), value);
    }

    static average(value: string) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'StatusAverage'), value);
    }

    static count(count: number) {
        return $eq.text.stringFormat($eq.str('SdkResources', 'StatusCount'), $eq.text.asNumber(count, 'int32'));
    }
}

