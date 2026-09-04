import { Box, BoxStyle, BuildContext, Button, Column, CornerRadii, EdgeInsets, Link, Row, SdkStrings, SizeValue, Spacer, StatefulComponent, Text } from "../runtime-exports";

export class CookieConsent extends StatefulComponent {
    declare policyHref: any;
    declare title: any;
    declare body: any;
    declare acceptLabel: any;
    declare rejectLabel: any;
    declare policyLabel: any;

    constructor(policyHref: any = null, props?: any) {
        super();
        if (policyHref !== undefined) this.policyHref = policyHref;
        if (this.policyHref === undefined) this.policyHref = this.policyHref;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let consent = context.getService('IConsent');
        if (consent == null || consent.state !== 'unknown') return Spacer.fixed(0);
        let theme = context.theme;
        let actions = new Row(8, 'start', 'center', false, null, null, { cross: 'center', wrap: true, runGap: 8 });
        actions.add(new Button(this.acceptLabel ?? SdkStrings.acceptCookies, 'primary', 'small', () => this.answer(consent.grant.bind(consent))));
        actions.add(new Button(this.rejectLabel ?? SdkStrings.rejectCookies, 'secondary', 'small', () => this.answer(consent.deny.bind(consent))));
        let href: any; 
        if (((this.policyHref != null && this.policyHref.length > 0) && (href = this.policyHref, true))) {
            actions.add(new Link(href, new Text(this.policyLabel ?? SdkStrings.privacyPolicy, 'labelSmall', theme.colors('primary').base, 1)));
        }
        let column = new Column(8, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        column.add(new Text(this.title ?? SdkStrings.cookieConsentTitle, 'label', theme.textPrimary, 2));
        column.add(new Text(this.body ?? SdkStrings.cookieConsentBody, 'bodyM', theme.textSecondary, 6));
        column.add(actions);
        return new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.all(16), background: theme.surface, borderWidth: 1, borderColor: theme.border, cornerRadius: new CornerRadii(theme.shape('large')), elevation: 2 }), column);
    }

    answer(reply: () => void) {
        return this.setState(reply);
    }
}

