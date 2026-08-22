import { $eq } from "../runtime-exports";

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

    static get spreadsheet(): string {
        return $eq.str("SdkResources", "Spreadsheet");
    }

    static get back(): string {
        return $eq.str("SdkResources", "Back");
    }

    static get nothingSelected(): string {
        return $eq.str("SdkResources", "NothingSelected");
    }
}

