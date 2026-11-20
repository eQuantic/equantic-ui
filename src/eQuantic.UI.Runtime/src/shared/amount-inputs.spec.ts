/**
 * MoneyInput, NumberInput and Currency in the browser (#1764): the TRANSPILED components, mounted in the
 * DOM under each culture the server installs, replay the scripts the C# side ran (AmountInputsFixtureTests)
 * and must show, say and hand on exactly what the server's run did. The fixture is a pin written from .NET:
 * its money is .NET's own `C` with the currency's symbol and digits, and its readings are `decimal.TryParse`
 * and `int.TryParse` in the culture, so this is the same C# answering on both sides.
 */

import { afterEach, describe, expect, it } from 'vitest';
import fixture from './amount-inputs.fixture.json';
import { StatefulComponent } from '../core/component';
import { installCulture, type CultureFormat } from '../utils/culture';
import { photonTheme } from './design-system.generated';
import { setPhotonTheme } from './photon-context';
import { Currency } from './components/Currency';
import { MoneyInput } from './components/MoneyInput';
import { NumberInput } from './components/NumberInput';

setPhotonTheme(photonTheme);

interface Seen {
  value: string;
  name: string | null;
  invalid: boolean;
  caption: string;
  prefix: string | null;
  suffix: string | null;
  handed: (string | null)[];
}

interface Script {
  kind: 'money' | 'number';
  currency: string;
  value: string | null;
  min: string | null;
  max: string | null;
  unit: string;
  steps: string[];
  seen: Seen[];
}

interface CultureCase {
  format: CultureFormat;
  formats: { currency: string; amount: string; text: string }[];
  scripts: Script[];
}

const cultures = fixture as unknown as Record<string, CultureCase>;

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

const currencyOf = (code: string): Currency =>
  code === 'JPY' ? Currency.yen : code === 'CHF' ? Currency.swissFranc : Currency.euro;

/** The page around the field: it keeps what the field hands on and hands it back on its next build. */
class Page extends StatefulComponent {
  handed: (string | null)[] = [];
  private value: bigint | number | null;

  constructor(private readonly script: Script) {
    super();
    const { value, kind } = script;
    this.value = value === null ? null : kind === 'money' ? BigInt(value) : Number(value);
  }

  override build(): never {
    const { kind, min, max, unit, currency } = this.script;
    const keep = (value: bigint | number | null): void =>
      this.setState(() => {
        this.handed.push(value === null ? null : String(value));
        this.value = value;
      });
    return (
      kind === 'money'
        ? new MoneyInput(this.value, keep, 'Price', currencyOf(currency),
            min === null ? null : BigInt(min), max === null ? null : BigInt(max))
        : new NumberInput(this.value, keep, 'Price', unit,
            min === null ? null : Number(min), max === null ? null : Number(max))
    ) as never;
  }
}

/** The text a node holds, or null for none. */
const textOf = (node: Element | null): string | null => {
  const text = node?.textContent ?? '';
  return text.length > 0 ? text : null;
};

/** What the page shows, as the C# side reads it from the server's render (AmountDriver.Observe). */
function observe(container: HTMLElement, page: Page): Seen {
  const input = container.querySelector('input')!;
  // The row the entry sits in: the nearest ancestor of its field with more than one child.
  let holder: Element = input.closest('.eq-field')!;
  while (holder.parentElement && holder.parentElement.children.length < 2) holder = holder.parentElement;
  return {
    value: input.value,
    name: input.getAttribute('aria-label'),
    invalid: input.getAttribute('aria-invalid') === 'true',
    caption: container.querySelector('[aria-live]')?.textContent ?? '',
    prefix: textOf(holder.previousElementSibling),
    suffix: textOf(holder.nextElementSibling),
    handed: [...page.handed],
  };
}

describe('money and numbers as the server writes and reads them (#1764)', () => {
  afterEach(() => installCulture('', '', {}));

  for (const [culture, { format, formats, scripts }] of Object.entries(cultures)) {
    const label = culture === '' ? 'the invariant culture' : culture;

    it(`writes money as .NET's C with the currency's symbol and digits, in ${label}`, () => {
      installCulture('', culture, {}, culture === '' ? null : format);
      for (const { currency, amount, text } of formats)
        expect(currencyOf(currency).format(BigInt(amount)), `${amount} ${currency}`).toBe(text);
    });

    scripts.forEach((script, index) => {
      it(`${script.kind} script ${index} shows and hands on what the server's did, in ${label}`, async () => {
        installCulture('', culture, {}, culture === '' ? null : format);
        const container = document.createElement('div');
        document.body.appendChild(container);
        const page = new Page(script);
        page.mount(container);
        await nextFrame();

        for (let step = 0; step < script.steps.length; step++) {
          const input = container.querySelector('input')!;
          const action = script.steps[step];
          if (action === 'focus') input.focus();
          else if (action === 'blur') input.blur();
          else if (action !== 'look') {
            // A person takes the focus before typing, as the C# driver does.
            if (document.activeElement !== input) input.focus();
            input.value = action;
            input.dispatchEvent(new Event('input', { bubbles: true }));
          }
          await nextFrame();
          expect(observe(container, page), `after "${action}"`).toEqual(script.seen[step]);
        }

        container.remove();
      });
    });
  }
});
