import { $eq, CSharpLanguage, JsonLanguage, PlainTextLanguage, PythonLanguage, TypeScriptLanguage, XmlLanguage } from "../runtime-exports";

export class CodeLanguages {
    static _cSharp: any | undefined;

    static get cSharp(): any {
        return CodeLanguages._cSharp ??= new CSharpLanguage();
    }

    static _typeScript: any | undefined;

    static get typeScript(): any {
        return CodeLanguages._typeScript ??= new TypeScriptLanguage();
    }

    static _python: any | undefined;

    static get python(): any {
        return CodeLanguages._python ??= new PythonLanguage();
    }

    static _json: any | undefined;

    static get json(): any {
        return CodeLanguages._json ??= new JsonLanguage();
    }

    static _xml: any | undefined;

    static get xml(): any {
        return CodeLanguages._xml ??= new XmlLanguage();
    }

    static _plainText: any | undefined;

    static get plainText(): any {
        return CodeLanguages._plainText ??= new PlainTextLanguage();
    }

    static _known: any | undefined;

    static get known(): any {
        return CodeLanguages._known ??= $eq.collections.dictionary().set('c#', CodeLanguages.cSharp).set('csharp', CodeLanguages.cSharp).set('cs', CodeLanguages.cSharp).set('typescript', CodeLanguages.typeScript).set('ts', CodeLanguages.typeScript).set('javascript', CodeLanguages.typeScript).set('js', CodeLanguages.typeScript).set('tsx', CodeLanguages.typeScript).set('jsx', CodeLanguages.typeScript).set('python', CodeLanguages.python).set('py', CodeLanguages.python).set('json', CodeLanguages.json).set('xml', CodeLanguages.xml).set('csproj', CodeLanguages.xml).set('html', CodeLanguages.xml).set('plist', CodeLanguages.xml).set('text', CodeLanguages.plainText).set('txt', CodeLanguages.plainText).set('plain', CodeLanguages.plainText);
    }

    static register(name: string, language: any) {
        return $eq.mapSet(CodeLanguages.known, CodeLanguages.keyOf(name), language);
    }

    static for(name: string | null) {
        if ((!$eq.text.hasNonWhiteSpace(name))) return CodeLanguages.plainText;
        let language: any; 
        return (($0: any, $1: any) => ($0.has($1) ? ((language = $0.get($1)), true) : ((language = null), false)))(CodeLanguages.known, CodeLanguages.keyOf(name)) ? language : CodeLanguages.plainText;
    }

    static keyOf(name: string) {
        return (_s => { const _c = '.'; let _i = 0; while (_i < _s.length && _c.includes(_s[_i])) _i++; return _s.slice(_i); })(name).toLowerCase();
    }
}

