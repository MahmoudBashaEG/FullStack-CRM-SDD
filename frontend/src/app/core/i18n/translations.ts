import type { EN } from './en';

type DeepStrings<T> = { readonly [K in keyof T]: T[K] extends string ? string : DeepStrings<T[K]> };

/** The shape every language must have. Arabic is typed with it, so a missing or extra key is a compile error (PLAT-001 D3). */
export type Translations = DeepStrings<typeof EN>;
