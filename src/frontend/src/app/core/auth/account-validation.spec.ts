import { describe, expect, it } from 'vitest';
import { passwordAccepted, displayNameAccepted, normalizeEmail, trimAccountWhitespace } from './account-validation';

describe('shared account validation', () => {
  it.each([' \t','\u0085','\u00a0\u202f'])('counts display-name scalars after canonical whitespace %j',whitespace=>{
    expect(displayNameAccepted(whitespace+'😀'.repeat(200)+whitespace)).toBe(true);
    expect(displayNameAccepted(whitespace+'😀'.repeat(201)+whitespace)).toBe(false);
    expect(displayNameAccepted(whitespace)).toBe(false);
  });
  it('preserves non-whitespace BOM and rejects malformed name scalars',()=>{
    expect(trimAccountWhitespace(' \u0085Name\u0085Inside\ufeff \u0085')).toBe('Name\u0085Inside\ufeff');
    expect(displayNameAccepted('\ufeff')).toBe(true);expect(displayNameAccepted('\ud800')).toBe(false);
    expect(normalizeEmail('\ufeffuser@example.test\ufeff')).toBe('\ufeffuser@example.test\ufeff');
  });
  it.each(['\u0085',' \t\r\n','\u00a0','\u1680','\u2000\u200a','\u2028\u2029','\u202f\u205f\u3000'])('trims backend email whitespace %j without changing internal characters',whitespace=>{
    expect(normalizeEmail(whitespace+'"multi part"@example.test'+whitespace)).toBe('"multi part"@example.test');
    expect(normalizeEmail(whitespace+'User@Example.test'+whitespace)).toBe('User@Example.test');
  });
  it('does not silently trim a BOM which .NET does not treat as whitespace',()=>expect(normalizeEmail('\ufeffuser@example.test')).toBe('\ufeffuser@example.test'));
  it.each(['"multi part"@example.test', '"part@inside"@example.test', '"part<>inside"@example.test', 'مريم@example.test'])('passes server-supported single addresses unchanged: %s', email => {
    expect(normalizeEmail(`  ${email}  `)).toBe(email);
  });
  it.each(['', 'bad', '@example.test', 'user@', 'user\u0085@example.test', 'user\ud800@example.test', 'a'.repeat(250)+'@example.test'])('rejects obvious invalid or unsafe input: %s', email => expect(normalizeEmail(email)).toBeNull());
  it('delegates full single-address syntax to the authoritative server',()=>expect(normalizeEmail('Name <user@example.test>')).toBe('Name <user@example.test>'));
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
