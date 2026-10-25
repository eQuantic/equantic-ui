import { FormulaBar, NameBox, SheetController, SizeValue, Spreadsheet } from "../runtime-exports";

export class SpreadsheetsUI {
    static spreadsheet(controller: SheetController, width?: SizeValue, height?: SizeValue, overscan: number = 3) {
        return new Spreadsheet(controller, { width: width, height: height, overscan: overscan });
    }

    static formulaBar(controller: SheetController, width: SizeValue | null = null) {
        return new FormulaBar(controller, { width: width ?? SizeValue.fill });
    }

    static nameBox(controller: SheetController, width: SizeValue | null = null) {
        return new NameBox(controller, { width: width ?? SizeValue.fixed(96) });
    }
}

