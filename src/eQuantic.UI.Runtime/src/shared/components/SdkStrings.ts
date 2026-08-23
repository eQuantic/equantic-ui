import { $eq, CalendarNames } from "../runtime-exports";

export class SdkStrings {
    static get dismiss(): string {
        return $eq.str("SdkResources", "Dismiss");
    }

    static get remove(): string {
        return $eq.str("SdkResources", "Remove");
    }

    static get searchPlaceholder(): string {
        return $eq.str("SdkResources", "SearchPlaceholder");
    }

    static get clearSearch(): string {
        return $eq.str("SdkResources", "ClearSearch");
    }

    static get find(): string {
        return $eq.str("SdkResources", "Find");
    }

    static get previousMatch(): string {
        return $eq.str("SdkResources", "PreviousMatch");
    }

    static get nextMatch(): string {
        return $eq.str("SdkResources", "NextMatch");
    }

    static get previousMonth(): string {
        return $eq.str("SdkResources", "PreviousMonth");
    }

    static get nextMonth(): string {
        return $eq.str("SdkResources", "NextMonth");
    }

    static get today(): string {
        return $eq.str("SdkResources", "Today");
    }

    static get chooseDate(): string {
        return $eq.str("SdkResources", "ChooseDate");
    }

    static get chooseTime(): string {
        return $eq.str("SdkResources", "ChooseTime");
    }

    static get dateFormatHint(): string {
        return SdkStrings.hint(CalendarNames.shortDatePattern, SdkStrings.dateFormatLetters);
    }

    static get dateFormatLetters(): string {
        return $eq.str("SdkResources", "DateFormatLetters");
    }

    static get spreadsheet(): string {
        return $eq.str("SdkResources", "Spreadsheet");
    }

    static get back(): string {
        return $eq.str("SdkResources", "Back");
    }

    static get nothingSelected(): string {
        return $eq.str("SdkResources", "NothingSelected");
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
                let close = pattern.indexOf('\'', i + 1);
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
}

