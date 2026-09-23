import { CodeEditorController, KeyboardConventionValue } from "../runtime-exports";

export class CodeKeymap {
    static handle(editor: CodeEditorController, key: string, modifiers: number, convention: KeyboardConventionValue, clipboard: any = null) {
        let shift = (modifiers & 1) !== 0;
        let command = (modifiers & 4) !== 0;
        let alt = (modifiers & 2) !== 0;
        let apple = convention === 'apple';
        if (key === 'Escape') {
            editor.tabMovesFocus = true;
            return false;
        }
        if (key !== 'Tab' && !CodeKeymap.isModifierKey(key)) editor.tabMovesFocus = false;
        if (command && key.length === 1) {
            switch (key[0].toLowerCase()) {
                case 'a':
                    editor.selectAll();
                    return true;
                case 'z':
                    return shift ? editor.redo() : editor.undo();
                case 'y':
                    return editor.redo();
                case 'c':
                    if (clipboard == null) return false;
                    clipboard.write(editor.copyText());
                    return true;
                case 'x':
                    if (clipboard == null) return false;
                    clipboard.write(editor.cut());
                    return true;
                case 'v':
                    if (clipboard == null) return false;
                    let pasted: any; 
                    if (((clipboard.read() != null && clipboard.read().length > 0) && (pasted = clipboard.read(), true))) editor.paste(pasted);
                    return true;
                case '/':
                    return editor.toggleLineComment();
                default:
                    return false;
            }
        }
        if (command && (key === '/' || key === 'Slash')) return editor.toggleLineComment();
        let byWord = apple ? alt : command;
        switch (key) {
            case 'ArrowLeft':
                editor.move(apple && command ? 'lineBoundary' : byWord ? 'word' : 'character', 'backward', shift);
                return true;
            case 'ArrowRight':
                editor.move(apple && command ? 'lineBoundary' : byWord ? 'word' : 'character', 'forward', shift);
                return true;
            case 'ArrowUp':
                if (!apple && command) return false;
                editor.move(apple && command ? 'documentBoundary' : 'line', 'backward', shift);
                return true;
            case 'ArrowDown':
                if (!apple && command) return false;
                editor.move(apple && command ? 'documentBoundary' : 'line', 'forward', shift);
                return true;
            case 'Home':
                editor.move(command ? 'documentBoundary' : 'lineBoundary', 'backward', shift);
                return true;
            case 'End':
                editor.move(command ? 'documentBoundary' : 'lineBoundary', 'forward', shift);
                return true;
            case 'PageUp':
                editor.move('page', 'backward', shift);
                return true;
            case 'PageDown':
                editor.move('page', 'forward', shift);
                return true;
        }
        switch (key) {
            case 'Enter':
                return editor.insertNewLine();
            case 'Backspace':
                return editor.deleteBackward(apple && command ? 'lineBoundary' : byWord ? 'word' : 'character');
            case 'Delete':
                return editor.deleteForward(byWord ? 'word' : 'character');
            case 'Tab':
                if (editor.readOnly || editor.tabMovesFocus) return false;
                return shift ? editor.outdent() : editor.indent();
            default:
                return false;
        }
    }

    static isModifierKey(key: string) {
        return (((((((key === 'Shift' || key === 'Control') || key === 'Alt') || key === 'Meta') || key === 'AltGraph') || key === 'CapsLock') || key === 'Fn') || key === 'OS');
    }
}

