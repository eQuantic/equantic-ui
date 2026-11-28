import { $eq, BuildContext, FocusNode, FocusScopeNode, StatefulComponent, UI } from "@equantic/runtime";

export class FocusSearch extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Web.Tests.Fixtures.FocusSearch';
    _field: FocusNode = new FocusNode();
    _results: FocusScopeNode = (() => { let $n0: FocusScopeNode; return ($n0 = new FocusScopeNode(), $n0.traversalEdgeBehavior = 'parentScope', $n0); })();
    _status: string = 'idle';
    _heard: number = 0;

    get status() {
        return this._status;
    }

    get heard() {
        return this._heard;
    }

    constructor(props?: any) {
        super();
        this._field.changed = $eq.delegates.combine(this._field.changed, () => this.setState(() => this._status = this._field.hasFocus ? 'field' : 'idle'));
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        let $n1: any; let $n2: any; let $n3: any; let $n4: any; 
        return UI.column(0, 'start', 'stretch', false, null, null, undefined, undefined, [UI.focus(($n1 = this._field, $n2 = UI.button('Search', 'primary', 'medium', $eq.delegates.bound(this, this.openResults)), $n2), $n1, true), UI.focusScope(($n3 = this._results, $n4 = UI.column(0, 'start', 'stretch', false, null, null, undefined, undefined, [UI.focus(UI.text(this._status), null, false, null, null, true)]), $n4), $n3, false, (hasFocus: boolean) => this._heard = this._heard + 1 | 0, 'parentScope')]);
    }

    openResults() {
        return this._results.requestFocus();
    }

    leave() {
        this._field.unfocus();
        this._field.nextFocus();
    }
}

