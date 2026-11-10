import { describe, expect, it } from 'vitest';
import { allowsScheme, writtenDestination } from './link-scheme';

/**
 * #1286: the C# table of `LinkSchemeTests`, against the twin. The parity case `links-with-schemes` holds the
 * two realizers to the same markup.
 */
describe('the schemes an href may carry', () => {
  it.each([
    '/pricing',
    'docs/next?page=2',
    '?page=2',
    '#top',
    '//cdn.example.com/x',
    'https://example.com',
    'HTTPS://example.com',
    'http://example.com',
    'mailto:team@example.com',
    'tel:+351210000000',
    '',
    'a/b:c',
    'javascript&colon;alert(1)',
  ])('writes %j as it is', (destination) => {
    expect(allowsScheme(destination)).toBe(true);
    expect(writtenDestination(destination)).toBe(destination);
  });

  it.each([
    'javascript:alert(1)',
    'JaVaScRiPt:alert(1)',
    ' javascript:alert(1)',
    '\u0001javascript:alert(1)',
    'java\tscript:alert(1)',
    'java\nscript:alert(1)',
    'java\rscript:alert(1)',
    'vbscript:msgbox',
    'data:text/html,<script>alert(1)</script>',
    'file:///etc/passwd',
    'ftp://example.com',
  ])('writes no href for %j', (destination) => {
    expect(allowsScheme(destination)).toBe(false);
    expect(writtenDestination(destination)).toBeNull();
  });
});
