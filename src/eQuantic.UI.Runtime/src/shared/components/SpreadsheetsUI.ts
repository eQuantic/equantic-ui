import { SheetController, SizeValue, Spreadsheet } from "../runtime-exports";

export class SpreadsheetsUI {
    static spreadsheet(controller: SheetController, width?: SizeValue, height?: SizeValue, overscan: number = 3) {
        return new Spreadsheet(controller, { width: width, height: height, overscan: overscan });
    }
}

