import { SheetController, SheetMotionValue } from "../runtime-exports";
export class SheetKeymap {
    static handle(sheet: SheetController, key: string, command: boolean, shift: boolean) {
        let motion: SheetMotionValue = command ? 'dataEdge' : 'cell';
        if (sheet.editing) {
            switch (key) {
                case 'Enter':
                    sheet.commitEdit();
                    sheet.step(shift ? -1 : 1, 0);
                    return true;
                case 'Tab':
                    sheet.commitEdit();
                    sheet.step(0, shift ? -1 : 1);
                    return true;
                case 'Escape':
                    sheet.cancelEdit();
                    return true;
                case 'Backspace':
                    sheet.eraseFromDraft();
                    return true;
                case 'ArrowUp':
                    sheet.commitEdit();
                    sheet.move(-1, 0);
                    return true;
                case 'ArrowDown':
                    sheet.commitEdit();
                    sheet.move(1, 0);
                    return true;
                case 'ArrowLeft':
                    sheet.commitEdit();
                    sheet.move(0, -1);
                    return true;
                case 'ArrowRight':
                    sheet.commitEdit();
                    sheet.move(0, 1);
                    return true;
            }
            return false;
        }
        switch (key) {
            case 'F2':
                sheet.beginEdit(sheet.document.getCell(sheet.activeCell));
                return true;
            case 'ArrowUp':
                sheet.move(-1, 0, motion, shift);
                return true;
            case 'ArrowDown':
                sheet.move(1, 0, motion, shift);
                return true;
            case 'ArrowLeft':
                sheet.move(0, -1, motion, shift);
                return true;
            case 'ArrowRight':
                sheet.move(0, 1, motion, shift);
                return true;
            case 'Tab':
                sheet.step(0, shift ? -1 : 1);
                return true;
            case 'Enter':
                sheet.step(shift ? -1 : 1, 0);
                return true;
            case 'Home':
                sheet.move(0, -1, command ? 'sheetBoundary' : 'rowBoundary', shift);
                return true;
            case 'End':
                sheet.move(0, 1, command ? 'sheetBoundary' : 'rowBoundary', shift);
                return true;
            case 'Backspace':
            case 'Delete':
                sheet.clearSelection();
                return true;
        }
        if (command && key.length === 1) {
            switch (key) {
                case 'a':
                case 'A':
                    sheet.selectAll();
                    return true;
                case 'z':
                    if (shift) sheet.redo(); else sheet.undo();
                    return true;
                case 'd':
                case 'D':
                    sheet.fillDown();
                    return true;
                case 'r':
                case 'R':
                    sheet.fillRight();
                    return true;
            }
        }
        return false;
    }

    static type(sheet: SheetController, text: string) {
        if (sheet.editing) sheet.typeIntoDraft(text); else sheet.beginEdit(text);
    }
}

