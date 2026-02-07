// eQuantic UI Theme Script
// Served via /_equantic/theme.js

window.__EQUANTIC_THEME_DATA = {{themeJson}};
window.__EQUANTIC_THEME_READY = false;

window.__registerTheme = function() {
    if (window.__EQUANTIC_THEME_READY) return;

    const themeData = window.__EQUANTIC_THEME_DATA;

    const variantMap = {
        0: 'default', 1: 'primary', 2: 'secondary', 3: 'destructive',
        4: 'outline', 5: 'ghost', 6: 'link', 7: 'success', 8: 'warning', 9: 'info', 10: 'custom'
    };

    const sizeMap = { 0: 'small', 1: 'medium', 2: 'large', 3: 'xlarge', 4: 'custom' };
    const shadowMap = { 0: 'none', 1: 'small', 2: 'medium', 3: 'large', 4: 'xlarge' };
    const cardVariantMap = { 0: 'default', 1: 'outline', 2: 'elevated', 3: 'subtle', 4: 'ghost' };

    const resolveKey = (val, map) => {
        if (val == null) return null;
        if (typeof val === 'number') return map[val] || val.toString().toLowerCase();
        if (typeof val === 'string') {
            const num = parseInt(val, 10);
            if (!isNaN(num) && map[num]) return map[num];
            return val.toLowerCase();
        }
        return val.toString().toLowerCase();
    };

    const theme = {
        button: {
            base: themeData.button.base,
            getVariant: (v) => themeData.button.variants[resolveKey(v, variantMap) || 'primary'] || themeData.button.variants.primary,
            getSize: (s) => themeData.button.sizes[resolveKey(s, sizeMap) || 'medium'] || themeData.button.sizes.medium
        },
        typography: {
            base: themeData.typography.base,
            getVariant: (v) => themeData.typography.variants[resolveKey(v, variantMap)] || '',
            getHeading: (level) => themeData.typography.headings[`h${level || 1}`] || themeData.typography.headings.h1
        },
        card: {
            container: themeData.card.container,
            header: themeData.card.header,
            body: themeData.card.body,
            footer: themeData.card.footer,
            title: themeData.card.title,
            description: themeData.card.description,
            getVariant: (v) => themeData.card.variants[resolveKey(v, cardVariantMap) || 'default'] || themeData.card.variants.default,
            getShadowInfo: (s) => themeData.card.shadows[resolveKey(s, shadowMap) || 'medium'] || themeData.card.shadows.medium
        },
        input: {
            base: themeData.input.base,
            getVariant: (v) => themeData.input.variants[resolveKey(v, variantMap) || 'default'] || '',
            getSize: (s) => themeData.input.sizes[resolveKey(s, sizeMap) || 'medium'] || themeData.input.sizes.medium
        },
        checkbox: {
            base: themeData.checkbox.base,
            root: themeData.checkbox.root,
            indicator: themeData.checkbox.indicator,
            checked: themeData.checkbox.checked,
            unchecked: themeData.checkbox.unchecked
        },
        badge: {
            base: themeData.badge.base,
            getVariant: (v) => themeData.badge.variants[resolveKey(v, variantMap) || 'default'] || themeData.badge.variants.default
        },
        alert: {
            base: themeData.alert.base,
            title: themeData.alert.title,
            description: themeData.alert.description,
            icon: themeData.alert.icon,
            getVariant: (v) => themeData.alert.variants[resolveKey(v, variantMap) || 'default'] || themeData.alert.variants.default
        },
        dialog: {
            overlay: themeData.dialog.overlay,
            content: themeData.dialog.content,
            header: themeData.dialog.header,
            title: themeData.dialog.title,
            description: themeData.dialog.description,
            footer: themeData.dialog.footer
        },
        table: {
            wrapper: themeData.table.wrapper,
            table: themeData.table.table,
            header: themeData.table.header,
            row: themeData.table.row,
            headCell: themeData.table.headCell,
            cell: themeData.table.cell
        },
        tabs: {
            list: themeData.tabs.list,
            trigger: themeData.tabs.trigger,
            content: themeData.tabs.content,
            activeTrigger: themeData.tabs.activeTrigger,
            inactiveTrigger: themeData.tabs.inactiveTrigger
        },
        avatar: {
            root: themeData.avatar.root,
            image: themeData.avatar.image,
            fallback: themeData.avatar.fallback,
            getSize: (s) => themeData.avatar.sizes[resolveKey(s, sizeMap) || 'medium'] || themeData.avatar.sizes.medium
        },
        switch: {
            root: themeData.switch.root,
            input: themeData.switch.input,
            thumb: themeData.switch.thumb,
            track: themeData.switch.track
        },
        select: {
            trigger: themeData.select.trigger,
            content: themeData.select.content,
            item: themeData.select.item,
            base: themeData.select.base
        }
    };

    getRootServiceProvider().registerInstance('IAppTheme', theme);
    getRootServiceProvider().registerInstance('eQuantic.UI.Core.Theme.IAppTheme', theme);
    window.__EQUANTIC_THEME_READY = true;
};
