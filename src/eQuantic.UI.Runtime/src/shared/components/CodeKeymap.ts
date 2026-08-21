import { CodeEditorController, CodeMotionValue } from "../runtime-exports";
export class CodeKeymap {
    static handle(editor: CodeEditorController, key: string, modifiers: number, clipboard: any = null) {
        let shift = (modifiers & 1) !== 0;
        let command = (modifiers & 4) !== 0;
        let alt = (modifiers & 2) !== 0;
        if (command && key.length === 1) {
            switch (key[0].toLowerCase()) { case 'a': editor.selectAll(); return true; case 'z': return shift ? editor.redo() : editor.undo(); case 'y': return editor.redo(); case 'c': clipboard?.write(editor.copyText()); return true; case 'x': clipboard?.write(editor.cut()); return true; case 'v': let pasted: any; 
            if (((clipboard?.read() != null && clipboard?.read().length > 0) && (pasted = clipboard?.read(), true))) editor.insert(pasted); return true; case '/': return editor.toggleLineComment(); default: return false; }
        }
        if (command && (key === '/' || key === 'Slash')) return editor.toggleLineComment();
        let motion: CodeMotionValue = alt ? 'word' : 'character';
        switch (key) { case 'ArrowLeft': editor.move(command ? 'lineBoundary' : motion, 'backward', shift); return true; case 'ArrowRight': editor.move(command ? 'lineBoundary' : motion, 'forward', shift); return true; case 'ArrowUp': editor.move(command ? 'documentBoundary' : 'line', 'backward', shift); return true; case 'ArrowDown': editor.move(command ? 'documentBoundary' : 'line', 'forward', shift); return true; case 'Home': editor.move(command ? 'documentBoundary' : 'lineBoundary', 'backward', shift); return true; case 'End': editor.move(command ? 'documentBoundary' : 'lineBoundary', 'forward', shift); return true; case 'PageUp': editor.move('page', 'backward', shift); return true; case 'PageDown': editor.move('page', 'forward', shift); return true; }
        switch (key) { case 'Enter': return editor.insertNewLine(); case 'Backspace': return editor.deleteBackward(alt ? 'word' : 'character'); case 'Delete': return editor.deleteForward(alt ? 'word' : 'character'); case 'Tab': if (editor.readOnly) return false; return shift ? editor.outdent() : editor.indent(); default: return false; }
    }
}

