/**
 * The form model's transpiled twin — the same C# the server validates with, running in the
 * browser. These expectations are CROSS-PINNED with FormModelTests.cs: the literals below appear
 * there too, asserted against the .NET compilation.
 *
 * Two of them exist because of what the transpiler got wrong the first time this model crossed:
 * `double.TryParse` with a culture overload emitted the number STYLE where the out variable
 * belonged, and a `Func<string, bool>` parameter reached TypeScript spelled in C#. Both are fixed
 * in the emitter, and these run the emitted code to keep them fixed.
 */

import { describe, expect, it } from 'vitest';
import { FormController } from './__transpiled__/FormController';
import { Rules } from './__transpiled__/Rules';

const signUp = () => {
  const form = new FormController();
  form.add('email', '', [Rules.required(), Rules.email()]);
  const password = form.add('password', '', [Rules.required(), Rules.minLength(8)]);
  form.add('confirm', '', [Rules.required(), Rules.matches(password)]);
  return form;
};

describe('the form model in the browser (C# cross-pin)', () => {
  it('computes an error immediately and shows it only once the field is left', () => {
    const form = signUp();
    const email = form.field('email')!;

    expect(email.error).not.toBeNull();
    expect(email.visibleError).toBeNull();

    email.touch();
    expect(email.visibleError).toBe('This field is required.');
  });

  it('follows every keystroke once touched', () => {
    const form = signUp();
    form.touch('email');

    form.set('email', 'ana@');
    expect(form.field('email')!.visibleError).toBe('Enter a valid email address.');

    form.set('email', 'ana@equantic.tech');
    expect(form.field('email')!.visibleError).toBeNull();
  });

  it('separates touched from dirty', () => {
    const form = new FormController();
    const name = form.add('name', 'Ana');

    name.touch();
    expect(name.touched).toBe(true);
    expect(name.dirty).toBe(false);

    name.set('Ana Beatriz');
    expect(form.dirty).toBe(true);
  });

  /** The rule whose emission was broken: a numeric range parsed through the culture overload. */
  it('reads a numeric range (the TryParse the emitter used to mangle)', () => {
    const form = new FormController();
    const age = form.add('age', '', [Rules.range(18, 120)]);

    age.set('42');
    expect(age.error).toBeNull();

    age.set('7');
    expect(age.error).toBe('Enter a number between 18 and 120.');

    age.set('not a number');
    expect(age.error).toBe('Enter a number between 18 and 120.');

    age.set('');
    expect(age.error).toBeNull();
  });

  /** The signature whose annotation was broken: a rule built from a bare predicate. */
  it('takes a custom predicate (the Func<string, bool> the emitter used to spell in C#)', () => {
    const form = new FormController();
    const coupon = form.add('coupon', '', [
      Rules.custom('Coupons start with EQ.', (value: string) => value.startsWith('EQ')),
    ]);

    coupon.set('EQ-2026');
    expect(coupon.error).toBeNull();

    coupon.set('SALE');
    expect(coupon.error).toBe('Coupons start with EQ.');
  });

  it('judges a cross-field rule against the value as it is NOW', () => {
    const form = signUp();
    form.set('password', 'correcthorse');
    form.set('confirm', 'correcthorse');
    expect(form.field('confirm')!.error).toBeNull();

    form.set('password', 'correcthorsebattery');
    expect(form.field('confirm')!.error).toBe('The two values do not match.');
  });

  it('reveals every error instead of doing nothing when an invalid form is submitted', async () => {
    const form = signUp();
    form.set('email', 'ana@equantic.tech');
    let ran = false;

    const ok = await form.submitAsync(async () => {
      ran = true;
    });

    expect(ok).toBe(false);
    expect(ran).toBe(false);
    expect(form.field('password')!.visibleError).toBe('This field is required.');
  });

  it('refuses a second submit while the first is in flight', async () => {
    const form = signUp();
    form.set('email', 'ana@equantic.tech');
    form.set('password', 'correcthorse');
    form.set('confirm', 'correcthorse');

    let release!: () => void;
    const gate = new Promise<void>((resolve) => (release = resolve));
    let runs = 0;

    const first = form.submitAsync(async () => {
      runs++;
      await gate;
    });
    const second = await form.submitAsync(async () => {
      runs++;
    });

    expect(second).toBe(false);
    expect(form.submitting).toBe(true);

    release();
    expect(await first).toBe(true);
    expect(runs).toBe(1);
    expect(form.submitting).toBe(false);
  });

  it('turns a throwing submit into form state', async () => {
    const form = signUp();
    form.set('email', 'ana@equantic.tech');
    form.set('password', 'correcthorse');
    form.set('confirm', 'correcthorse');

    const ok = await form.submitAsync(async () => {
      throw new Error('Service unavailable');
    });

    expect(ok).toBe(false);
    expect(form.submitError).toBe('Service unavailable');
    expect(form.submitting).toBe(false);
  });
});
