import { $eq, BuildContext, StatelessComponent, Text } from "@equantic/runtime";

export class BoundReceivers extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Web.Tests.Fixtures.BoundReceivers';
    _items: number[] = [3, 1, 2];
    _pairs: any[] = [[1, 'a'], [1, 'a'], [2, 'b']];
    _counts: any = $eq.collections.dictionary().set('a', 1);
    _seen: boolean = false;

    build(_context: BuildContext) {
        let average = (($0) => ($0.reduce(($a, $b) => $a + $b, 0) / $0.length))(this.items());
        let doubled = (($0, $1) => ($0.reduce(($sum, $x) => $sum + $1($x), 0) / $0.length))(this.items(), BoundReceivers.twice);
        let sum = this.items().reduce(($a, $b) => $a + $b, 0);
        let total = this.pairs().reduce(($sum, pair) => $sum + pair[0], 0);
        let first = (($0, $1) => [...$0].sort(($a, $b) => { { const $k: ($element: typeof $a) => any = $1; const $x = $k($a), $y = $k($b); if ($x < $y) return -1; if ($x > $y) return 1; } return 0; }))(this.pairs(), BoundReceivers.keyOf)[0][1];
        let distinct = this.pairs().filter((($seen) => ($x) => { const $k = JSON.stringify($x); if ($seen.has($k)) return false; $seen.add($k); return true; })(new Set())).length;
        (($0) => ($0._seen = $eq.logic.or($0._seen, distinct > 1)))(this.self());
        let value: any; 
        let count = (($0) => ($0.has(first) ? ((value = $0.get(first)), true) : ((value = 0), false)))(this._counts) ? value : 0;
        return new Text(`${$eq.num.double(average)} ${$eq.num.double(doubled)} ${sum} ${total} ${first} ${distinct} ${$eq.text.format(this._seen, null)} ${count}`, 'caption');
    }

    items() {
        return this._items;
    }

    pairs() {
        return this._pairs;
    }

    self() {
        return this;
    }

    static twice(value: number) {
        return value * 2;
    }

    static keyOf(pair: any) {
        return pair[0];
    }
}

