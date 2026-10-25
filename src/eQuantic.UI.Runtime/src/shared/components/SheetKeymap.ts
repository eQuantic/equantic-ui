import { $eq, KeyboardConventionValue, SheetController, SheetMotionValue } from "../runtime-exports";

export class SheetKeymap {
    static handle(sheet: SheetController, key: string, modifiers: number, convention: KeyboardConventionValue, clipboard: any = null) {
        if (SheetKeymap.isModifierKey(key)) return false;
        if (key !== 'Tab' && key !== 'Escape') sheet.tabReleased = false;
        let command = (modifiers & 4) !== 0;
        let shift = (modifiers & 1) !== 0;
        if (command && !(clipboard == null) && key.length === 1 && (modifiers & 2) === 0) {
            {
                let copied: any; let cut: any; let pasted: any;
                switch ($eq.text.toLower(key[0], true)) {
                    case 'c':
                        if ((($v2566) => (($v2566 != null && $v2566.length > 0) && (copied = $v2566, true)))(sheet.copyText())) clipboard.write(copied);
                        return true;
                    case 'x':
                        if ((($v2902) => !((($v2902 != null && $v2902.length > 0) && (cut = $v2902, true))))(sheet.copyText())) return true;
                        clipboard.write(cut);
                        sheet.cut();
                        return true;
                    case 'v':
                        if ((($v3118) => (($v3118 != null && $v3118.length > 0) && (pasted = $v3118, true)))(clipboard.read())) sheet.paste(pasted);
                        return true;
                }
            }
        }
        let time: any; 
        if (($o => (time = $o.time, $o.$))(SheetKeymap.isDateOrTime(key, modifiers, convention))) {
            sheet.typeNow(time);
            return true;
        }
        return sheet.editing ? SheetKeymap.handleEntry(sheet, key, modifiers, convention) : SheetKeymap.handleAtRest(sheet, key, modifiers, convention, command, shift);
    }

    static handleEntry(sheet: SheetController, key: string, modifiers: number, convention: KeyboardConventionValue) {
        let shift = (modifiers & 1) !== 0;
        let plain = (modifiers & ~1) === 0;
        {
            const $s = key;
            if (($s === 'Enter' && (modifiers === 2))) {
                sheet.breakLine();
                return true;
            } else if (($s === 'Enter' && (modifiers === 4))) {
                sheet.fillSelectionWithEntry();
                return true;
            } else if (($s === 'Enter' && (plain))) {
                sheet.commitEdit();
                sheet.step(shift ? -1 : 1, 0);
                return true;
            } else if (($s === 'Tab' && (plain))) {
                sheet.commitEdit();
                sheet.step(0, shift ? -1 : 1);
                return true;
            } else if ($s === 'Escape') {
                sheet.cancelEdit();
                return true;
            } else if (($s === 'F2' && (modifiers === 0))) {
                sheet.toggleEditMode();
                return true;
            }
        }
        let [dRow, dCol] = SheetKeymap.arrow(key);
        if (sheet.editMode === 'enter' && plain && (dRow !== 0 || dCol !== 0)) {
            sheet.commitEdit();
            sheet.move(dRow, dCol, 'cell', shift);
            return true;
        }
        let claimed = sheet.entryKey(key, modifiers, convention);
        return claimed || SheetKeymap.isUndoChord(key, modifiers);
    }

    static handleAtRest(sheet: SheetController, key: string, modifiers: number, convention: KeyboardConventionValue, command: boolean, shift: boolean) {
        if (SheetKeymap.isGoTo(key, modifiers, convention)) {
            if (!sheet.nameBox.isPlaced) return false;
            sheet.nameBox.requestFocus();
            return true;
        }
        let motion: SheetMotionValue = command ? 'dataEdge' : 'cell';
        {
            const $s = key;
            if ($s === 'F2') {
                sheet.beginEdit(sheet.document.getCell(sheet.activeCell), 'edit');
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
                if (sheet.tabReleased) return false;
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
            } else if (($s === 'Backspace' && (modifiers === 0))) {
                sheet.beginEdit('', 'enter');
                return true;
            } else if ($s === 'Delete') {
                sheet.clearSelection();
                return true;
            } else if ($s === 'Escape') {
                if (sheet.addingRanges) {
                    sheet.addingRanges = false;
                    return true;
                }
                sheet.tabReleased = true;
                return false;
            } else if (($s === 'F8' && (modifiers === 1))) {
                sheet.addingRanges = !sheet.addingRanges;
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
                case 'Z':
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

    static isDateOrTime(key: string, modifiers: number, convention: KeyboardConventionValue) {
        let time: any;
        const $r = (() => {
            time = false;
            if (!((key === ';' || key === ':'))) return false;
            if (convention === 'apple') {
                if (modifiers === 8) return true;
                time = true;
                return modifiers === 4;
            }
            if (modifiers === 4) return true;
            time = true;
            return modifiers === (4 | 1);
        })();
        return { $: $r, time };
    }

    static isGoTo(key: string, modifiers: number, convention: KeyboardConventionValue) {
        return key === 'F5' && modifiers === 0 || (key === 'g' || key === 'G') && modifiers === (convention === 'apple' ? 8 : 4);
    }

    static isUndoChord(key: string, modifiers: number) {
        return (modifiers & 4) !== 0 && (((key === 'z' || key === 'Z') || key === 'y') || key === 'Y');
    }

    static arrow(key: string): [number, number] {
        return (() => { const $s = key; if ($s === 'ArrowUp') return [-1, 0]; if ($s === 'ArrowDown') return [1, 0]; if ($s === 'ArrowLeft') return [0, -1]; if ($s === 'ArrowRight') return [0, 1]; return [0, 0]; })();
    }

    static isModifierKey(key: string) {
        return (((((((((key === 'Shift' || key === 'Control') || key === 'Alt') || key === 'AltGraph') || key === 'Meta') || key === 'OS') || key === 'CapsLock') || key === 'Fn') || key === 'Hyper') || key === 'Super');
    }
}

