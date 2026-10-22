import { $eq, SheetController, SheetMotionValue } from "../runtime-exports";

export class SheetKeymap {
    static handle(sheet: SheetController, key: string, modifiers: number, clipboard: any = null) {
        let command = (modifiers & 4) !== 0;
        let shift = (modifiers & 1) !== 0;
        if (command && !(clipboard == null) && key.length === 1) {
            {
                let pasted: any;
                switch ($eq.text.toLower(key[0], true)) {
                    case 'c':
                        clipboard.write(sheet.copyText());
                        return true;
                    case 'x':
                        clipboard.write(sheet.copyText());
                        sheet.clearSelection();
                        return true;
                    case 'v':
                        if ((($v2302) => (($v2302 != null && $v2302.length > 0) && (pasted = $v2302, true)))(clipboard.read())) sheet.paste(pasted);
                        return true;
                }
            }
        }
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
        {
            const $s = key;
            if ($s === 'F2') {
                sheet.beginEdit(sheet.document.getCell(sheet.activeCell));
                return true;
            } else if ($s === 'ArrowUp') {
                sheet.move(-1, 0, motion, shift);
                return true;
            } else if ($s === 'ArrowDown') {
                sheet.move(1, 0, motion, shift);
                return true;
            } else if ($s === 'ArrowLeft') {
                sheet.move(0, -1, motion, shift);
                return true;
            } else if ($s === 'ArrowRight') {
                sheet.move(0, 1, motion, shift);
                return true;
            } else if ($s === 'Tab') {
                sheet.step(0, shift ? -1 : 1);
                return true;
            } else if ($s === 'Enter') {
                sheet.step(shift ? -1 : 1, 0);
                return true;
            } else if ($s === 'Home') {
                sheet.move(0, -1, command ? 'sheetBoundary' : 'rowBoundary', shift);
                return true;
            } else if ($s === 'End') {
                sheet.move(0, 1, command ? 'sheetBoundary' : 'rowBoundary', shift);
                return true;
            } else if (($s === 'PageUp' && (!command))) {
                return sheet.movePage(-1, shift);
            } else if (($s === 'PageDown' && (!command))) {
                return sheet.movePage(1, shift);
            } else if ($s === 'Backspace' || $s === 'Delete') {
                sheet.clearSelection();
                return true;
            }
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

