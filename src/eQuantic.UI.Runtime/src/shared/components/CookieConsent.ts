import { Box, BoxStyle, BuildContext, Button, Column, CornerRadii, EdgeInsets, Link, Row, SdkStrings, SizeValue, Spacer, StatefulComponent, Text, UiComponent } from "../runtime-exports";

export class CookieConsent extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.CookieConsent';
    _config: CookieConsent;
    declare policyHref: any;
    declare title: any;
    declare body: any;
    declare acceptLabel: any;
    declare rejectLabel: any;
    declare policyLabel: any;

    constructor(policyHref: any = null, props?: any) {
        super();
        if (policyHref !== undefined) this.policyHref = policyHref;
        this.policyHref = policyHref;
        this._config = this;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let consent = context.getService('IConsent');
        if (consent == null || consent.state !== 'unknown') return Spacer.fixed(0);
        let theme = context.theme;
        let config = this._config;
        let actions = new Row(8, 'start', 'center', false, null, null, { cross: 'center', wrap: true, runGap: 8 });
        actions.add(new Button(config.acceptLabel ?? SdkStrings.acceptCookies, 'primary', 'small', () => this.answer(consent.grant.bind(consent))));
        actions.add(new Button(config.rejectLabel ?? SdkStrings.rejectCookies, 'secondary', 'small', () => this.answer(consent.deny.bind(consent))));
        let href: any; 
        if (((config.policyHref != null && config.policyHref.length > 0) && (href = config.policyHref, true))) {
            actions.add(new Link(href, new Text(config.policyLabel ?? SdkStrings.privacyPolicy, 'labelSmall', theme.colors('primary').base, 1)));
        }
        let column = new Column(8, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        column.add(new Text(config.title ?? SdkStrings.cookieConsentTitle, 'label', theme.textPrimary, 2));
        column.add(new Text(config.body ?? SdkStrings.cookieConsentBody, 'bodyM', theme.textSecondary, 6));
        column.add(actions);
        return new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.all(16), background: theme.surface, borderWidth: 1, borderColor: theme.border, cornerRadius: new CornerRadii(theme.shape('large')), elevation: 2 }), column);
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if ((next instanceof CookieConsent && (fresh = next, true))) this._config = fresh;
    }

    answer(reply: () => void) {
        return this.setState(reply);
    }
}

