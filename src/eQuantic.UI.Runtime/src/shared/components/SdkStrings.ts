import { $eq } from "../runtime-exports";
export class SdkStrings {
    static get checked(): string { return $eq.str("SdkResources", "Checked"); }
    static get unchecked(): string { return $eq.str("SdkResources", "Unchecked"); }
    static get partlySelected(): string { return $eq.str("SdkResources", "PartlySelected"); }
    static get on(): string { return $eq.str("SdkResources", "On"); }
    static get off(): string { return $eq.str("SdkResources", "Off"); }
    static get dismiss(): string { return $eq.str("SdkResources", "Dismiss"); }
    static get remove(): string { return $eq.str("SdkResources", "Remove"); }
    static get searchPlaceholder(): string { return $eq.str("SdkResources", "SearchPlaceholder"); }
    static get clearSearch(): string { return $eq.str("SdkResources", "ClearSearch"); }
    static get find(): string { return $eq.str("SdkResources", "Find"); }
    static get previousMatch(): string { return $eq.str("SdkResources", "PreviousMatch"); }
    static get nextMatch(): string { return $eq.str("SdkResources", "NextMatch"); }
    static get spreadsheet(): string { return $eq.str("SdkResources", "Spreadsheet"); }
}

