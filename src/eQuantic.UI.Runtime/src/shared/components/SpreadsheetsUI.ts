import { FormulaBar, NameBox, SheetController, SizeValue, Spreadsheet, StatusBar } from "../runtime-exports";

export class SpreadsheetsUI {
    static spreadsheet(controller: SheetController, width?: SizeValue, height?: SizeValue, overscan: number = 3) {
        return new Spreadsheet(controller, { width: width, height: height, overscan: overscan });
    }

    static formulaBar(controller: SheetController) {
        return new FormulaBar(controller);
    }

    static nameBox(controller: SheetController) {
        return new NameBox(controller);
    }

    static statusBar(controller: SheetController) {
        return new StatusBar(controller);
    }
}

