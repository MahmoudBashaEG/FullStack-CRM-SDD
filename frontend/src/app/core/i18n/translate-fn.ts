import { EN } from './en';

export type TranslateParams = Record<string, string | number>;
export type Translate = (key: string, params?: TranslateParams) => string;

export function lookup(dictionary: object, key: string, params?: TranslateParams): string | null {
  const value = key.split('.').reduce<unknown>((node, part) => (node as Record<string, unknown> | undefined)?.[part], dictionary);
  if (typeof value !== 'string') {
    return null;
  }
  return params ? value.replace(/\{(\w+)\}/g, (match, name: string) => (name in params ? String(params[name]) : match)) : value;
}

/** Default for label helpers (PLAT-001 D15): today's English, so callers and specs without a translator stay byte-identical. */
export const englishT: Translate = (key, params) => lookup(EN, key, params) ?? key;
