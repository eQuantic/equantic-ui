import { Box, BoxStyle, BuildContext, Button, CornerRadii, Motion, Pressable, Row, SizeValue, StatelessComponent, StyleDiff, Text, TransitionSpec } from "@equantic/runtime";

export class Pagination extends StatelessComponent {
    static window: number = 1;
    static alwaysShow: number = 7;
    static cell: number = 30;
    declare pageCount: number;
    declare currentPage: number;
    declare onChanged: any;
    constructor(pageCount?: any, currentPage?: any, onChanged: any = null, props?: any) {
        super();
        if (pageCount !== undefined) this.pageCount = pageCount;
        if (currentPage !== undefined) this.currentPage = currentPage;
        if (onChanged !== undefined) this.onChanged = onChanged;
        this.pageCount = pageCount;this.currentPage = currentPage;this.onChanged = onChanged;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let current = Math.min(Math.max(this.currentPage, 1), Math.max(1, this.pageCount));let numbers = new Row(3, { cross: 'center' });let previous = 0;for (const page of Pagination.pages(this.pageCount, current)) {if (previous > 0 && page > previous + 1) numbers.add(Pagination.ellipsis(theme));numbers.add(this.number(theme, page, page === current));previous = page;}let row = new Row(8, { cross: 'center' });row.add(Pagination.step('Previous', current > 1, () => this.onChanged?.(current - 1)));row.add(numbers);row.add(Pagination.step('Next', current < this.pageCount, () => this.onChanged?.(current + 1)));return row;
    }

    static pages(pageCount: number, currentPage: number) {
        if (pageCount <= 0) return [];if (pageCount <= Pagination.alwaysShow) {let all = new Array(pageCount).fill(0);for (let page = 1; page <= pageCount; page++) all[page - 1] = page;return all;}let current = Math.min(Math.max(currentPage, 1), pageCount);let pages = [1];for (let page = current - Pagination.window; page <= current + Pagination.window; page++) if (page > 1 && page < pageCount && !pages.includes(page)) pages.push(page);if (pageCount > 1) pages.push(pageCount);let ordered = new Array(pages.length).fill(0);for (let i = 0; i < pages.length; i++) ordered[i] = pages[i];return ordered;
    }

    number(theme: any, page: number, current: boolean) {
        let centered = new Row(0, { width: SizeValue.fill, height: SizeValue.fill, main: 'center', cross: 'center' });centered.add(new Text(String(page), 'caption', current ? theme.colors('primary').onBase : theme.textSecondary, 1, { tabular: true }));let box = new Box(new BoxStyle({ width: Pagination.cell, height: Pagination.cell, background: current ? theme.colors('primary').base : null, cornerRadius: new CornerRadii(theme.shape('small')), hover: current ? null : new StyleDiff({ background: theme.surfaceSubtle }), transition: TransitionSpec.of(1, Motion.press) }), centered);return new Pressable(box, current ? null : () => this.onChanged?.(page), { label: `Page ${page}`, selected: current });
    }

    static ellipsis(theme: any) {
        let centered = new Row(0, { width: SizeValue.fill, height: SizeValue.fill, main: 'center', cross: 'center' });centered.add(new Text('…', 'caption', theme.textMuted, 1));return new Box(new BoxStyle({ width: Pagination.cell, height: Pagination.cell }), centered);
    }

    static step(label: string, enabled: boolean, onPressed: () => void) {
        return new Button(label, 'outline', 'small', null, { disabled: !enabled, onPressed: onPressed });
    }

}

