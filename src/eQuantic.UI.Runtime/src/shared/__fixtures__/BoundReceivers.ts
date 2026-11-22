import { $eq, BuildContext, Decimal, StatelessComponent, Text } from "@equantic/runtime";

export class BoundReceivers extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Web.Tests.Fixtures.BoundReceivers';
    _items: number[] = [3, 1, 2];
    _pairs: any[] = [[1, 'a'], [1, 'a'], [2, 'b']];
    _counts: any = $eq.collections.dictionary().set('a', 1);
    _ticks: bigint[] = [4n, 6n];
    _prices: Decimal[] = [$eq.num.dec("1.5"), $eq.num.dec("2.25")];
    _seen: boolean = false;
    _level: number = 1;

    build(_context: BuildContext) {
        let average = $eq.linq.average(this.items(), undefined, 'int', false);
        let doubled = $eq.linq.average(this.items(), BoundReceivers.twice, 'int', false);
        let sum = this.items().reduce(($a, $b) => $eq.num.addInt($a, $b), 0);
        let total = this.pairs().reduce(($sum, pair) => $eq.num.addInt($sum, pair[0]), 0);
        let first = $eq.query.first((($0, $1) => $eq.query.defer(($src) => [...$src].sort(($a, $b) => { { const $k: ($element: typeof $a) => any = $1; const $x = $k($a), $y = $k($b); if ($x < $y) return -1; if ($x > $y) return 1; } return 0; }), $0))(this.pairs(), BoundReceivers.keyOf))[1];
        let distinct = $eq.linq.seq($eq.query.defer(($src) => [...$eq.collections.hashSet(true, $src)], this.pairs())).length;
        (($0) => ($0._seen = $eq.logic.or($0._seen, distinct > 1)))(this.self());
        let value: any; 
        let count = (($0) => ($0.has(first) ? ((value = $0.get(first)), true) : ((value = 0), false)))(this._counts) ? value : 0;
        let groups = $eq.linq.seq((($0, $1) => $eq.query.defer(($src) => $src.reduce(($groups: any[], $item) => { const $key = ($1)($item); let $g = $groups.find(($x) => $x.key === $key); if (!$g) { $g = []; $g.key = $key; $groups.push($g); } $g.push($item); return $groups; }, []), $0))(this.items(), BoundReceivers.twice)).length;
        let lookup = (($0, $1) => $0.reduce(($groups: any[], $item) => { const $key = ($1)($item); let $g = $groups.find(($x) => $x.key === $key); if (!$g) { $g = []; $g.key = $key; $groups.push($g); } $g.push($item); return $groups; }, []))(this.pairs(), BoundReceivers.keyOf);
        let named = this.pairs().reduce(($groups: any[], $item) => { const $key = ((pair) => pair[0])($item); let $g = $groups.find(($x) => $x.key === $key); if (!$g) { $g = []; $g.key = $key; $groups.push($g); } $g.push(((pair) => pair[1])($item)); return $groups; }, []);
        let found = $eq.linq.seq((lookup.find(($g) => $g.key === 1) ?? [])).length + $eq.linq.seq((named.find(($g) => $g.key === 2) ?? [])).length | 0;
        let zipped = $eq.linq.seq((($0, $1) => $eq.query.defer(($src) => $eq.linq.zipTuples($src, $1, undefined), $0, 'first'))(this.items(), this.items())).reduce(($sum, pair) => $eq.num.addInt($sum, Math.imul(pair[0], pair[1])), 0);
        let triples = $eq.linq.seq((($0, $1, $2) => $eq.query.defer(($src) => $eq.linq.zipTuples($src, $1, $2), $0, 'first'))(this.items(), this.items(), this.pairs())).reduce(($sum, triple) => $eq.num.addInt($sum, triple[2][0]), 0);
        let same = $eq.linq.sequenceEqual(this.items(), this.items());
        let ticks = $eq.linq.seq($eq.query.select(this._ticks, (tick: bigint) => BigInt.asIntN(64, tick * 2n))).reduce(($a, $b) => $eq.num.addLong($a, $b), 0n);
        let price = $eq.linq.average(this._prices, (each: Decimal) => each, 'decimal', false);
        let fallback = ($eq.linq.firstOrDefault(this.items(), undefined, 9) + $eq.linq.lastOrDefault(this.items(), (item: number) => item > 1, 9) | 0) + $eq.linq.singleOrDefault(this.items(), (item: number) => item > 2, 9) | 0;
        let discarded = ((void (-1), true)) && ((void (!this._seen), true)) && ((void (this._level + 1 | 0), true)) && ((void this.items().length, true));
        return new Text(`${$eq.text.format(average, null)} ${$eq.text.format(doubled, null)} ${$eq.text.format(sum, null, undefined, undefined, 'int32')} ${$eq.text.format(total, null, undefined, undefined, 'int32')} ${first} ${$eq.text.format(distinct, null, undefined, undefined, 'int32')} ${$eq.text.format(this._seen, null)} ${$eq.text.format(count, null, undefined, undefined, 'int32')} ` + `${$eq.text.format(groups, null, undefined, undefined, 'int32')} ${$eq.text.format(found, null, undefined, undefined, 'int32')} ${$eq.text.format(zipped, null, undefined, undefined, 'int32')} ${$eq.text.format(triples, null, undefined, undefined, 'int32')} ${$eq.text.format(same, null)} ${$eq.text.format(fallback, null, undefined, undefined, 'int32')} ${$eq.text.format(ticks, null)} ${$eq.text.format(price, null)} ${$eq.text.format(discarded, null)}`, 'caption');
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
        return Math.imul(value, 2);
    }

    static keyOf(pair: any) {
        return pair[0];
    }
}

