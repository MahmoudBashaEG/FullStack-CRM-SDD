import { describe, expect, it } from 'vitest';
import { passwordAccepted, displayNameAccepted, normalizeEmail } from './account-validation';

describe('shared account validation', () => {
  it.each([[14,false],[15,true],[128,true],[129,false]])('password scalar boundary %s', (length, valid) => expect(passwordAccepted('a'.repeat(Number(length)))).toBe(valid));
  it('allows spaces and supplementary Unicode without normalizing passwords', () => {
    expect(passwordAccepted(' '.repeat(15))).toBe(true);
    expect(passwordAccepted('😀'.repeat(128))).toBe(true);
    expect(passwordAccepted('😀'.repeat(129))).toBe(false);
    expect(passwordAccepted('\ud800'.repeat(15))).toBe(false);
  });
  it('trims names only and normalizes email identity', () => {
    expect(displayNameAccepted('  مريم  ')).toBe(true);
    expect(displayNameAccepted(' '.repeat(20))).toBe(false);
    expect(displayNameAccepted('😀'.repeat(200))).toBe(true);
    expect(displayNameAccepted('a'.repeat(201))).toBe(false);
    expect(normalizeEmail(' A@EXAMPLE.TEST ')).toBe('A@EXAMPLE.TEST');
    expect(normalizeEmail('bad')).toBe(null);
  });
});
