import { $eq, Box, BoxStyle, BuildContext, Button, CornerRadii, Motion, Pressable, Row, SdkStrings, SizeValue, StatelessComponent, StyleDiff, Text, TransitionSpec } from "../runtime-exports";

export class Pagination extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.Pagination';
    static window: number = 1;
    static alwaysShow: number = 7;
    declare pageCount: number;
    declare currentPage: number;
    declare onChanged: ((int: number) => void) | null;

    constructor(pageCount?: any, currentPage?: any, onChanged: any = null, props?: any) {
        super();
        if (pageCount !== undefined) this.pageCount = pageCount;
        if (currentPage !== undefined) this.currentPage = currentPage;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (this.pageCount === undefined) this.pageCount = 0;
        if (this.currentPage === undefined) this.currentPage = 0;
        this.pageCount = pageCount;
        this.currentPage = currentPage;
        this.onChanged = onChanged;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let current = $eq.num.clamp(this.currentPage, 1, Math.max(1, this.pageCount), 'int32');
        let slot = theme.navigation;
        let numbers = new Row(slot?.pageGap ?? 3, 'start', 'center', false, null, null, { cross: 'center' });
        let previous = 0;
        for (const page of Pagination.pages(this.pageCount, current)) {
            if (previous > 0 && page > (previous + 1 | 0)) numbers.add(Pagination.ellipsis(theme));
            numbers.add(this.number(theme, page, page === current));
            previous = page;
        }
        let row = new Row(slot?.paginationGap ?? 8, 'start', 'center', false, null, null, { cross: 'center' });
        row.add(Pagination.step(SdkStrings.previousPage, current > 1, () => this.onChanged?.(current - 1 | 0)));
        row.add(numbers);
        row.add(Pagination.step(SdkStrings.nextPage, current < this.pageCount, () => this.onChanged?.(current + 1 | 0)));
        return row;
    }

    static pages(pageCount: number, currentPage: number) {
        if (pageCount <= 0) return [];
        if (pageCount <= Pagination.alwaysShow) {
            let all = new Array(pageCount).fill(0);
            for (let page = 1; page <= pageCount; page = page + 1 | 0) all[page - 1 | 0] = page;
            return all;
        }
        let current = $eq.num.clamp(currentPage, 1, pageCount, 'int32');
        let pages = [1];
        for (let page = current - Pagination.window | 0; page <= (current + Pagination.window | 0); page = page + 1 | 0) if (page > 1 && page < pageCount && !pages.includes(page)) $eq.collections.listAdd(pages, page);
        if (pageCount > 1) $eq.collections.listAdd(pages, pageCount);
        let ordered = new Array(pages.length).fill(0);
        for (let i = 0; i < pages.length; i = i + 1 | 0) ordered[i] = pages[i];
        return ordered;
    }

    number(theme: any, page: number, current: boolean) {
        let slot = theme.navigation;
        let cell = slot?.pageCellSize ?? 30;
        let centered = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: SizeValue.fill, main: 'center', cross: 'center' });
        centered.add(new Text($eq.text.format(page, null, undefined, undefined, 'int32'), 'caption', current ? theme.colors('primary').onBase : slot?.pageColor ?? theme.textSecondary, 1, null, false, false, null, 0, { tabular: true }));
        let box = new Box(new BoxStyle({ width: cell, height: cell, background: current ? theme.colors('primary').base : null, cornerRadius: new CornerRadii(slot?.pageRadius ?? theme.shape('small')), hover: current ? null : new StyleDiff({ background: slot?.quietFill ?? theme.surfaceSubtle }), transition: TransitionSpec.of(1, Motion.press) }), centered);
        return new Pressable(box, current ? null : () => this.onChanged?.(page), { label: SdkStrings.pageNumbered(page), selected: current });
    }

    static ellipsis(theme: any) {
        let slot = theme.navigation;
        let cell = slot?.pageCellSize ?? 30;
        let centered = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: SizeValue.fill, main: 'center', cross: 'center' });
        centered.add(new Text('…', 'caption', slot?.ellipsisColor ?? theme.textMuted, 1));
        return new Box(new BoxStyle({ width: cell, height: cell }), centered);
    }

    static step(label: string, enabled: boolean, onPressed: () => void) {
        return new Button(label, 'outline', 'small', null, { disabled: !enabled, onPressed: onPressed });
    }
}

