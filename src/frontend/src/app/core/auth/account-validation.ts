// Match .NET String.Trim: Unicode White_Space includes NEL and excludes BOM.
export const trimAccountWhitespace = (value: string): string =>
  value.replace(/^\p{White_Space}+|\p{White_Space}+$/gu, '');
function scalarLength(value: string): number | null {
  let count = 0;
  for (const character of value) {
    const code = character.codePointAt(0)!;
    if (code >= 0xd800 && code <= 0xdfff) return null;
    count++;
  }
  return count;
}
export const passwordAccepted = (value: string): boolean => {
  const length = scalarLength(value);
  return length !== null && length >= 15 && length <= 128;
};
export const displayNameAccepted = (value: string): boolean => {
  const trimmed = trimAccountWhitespace(value), length = scalarLength(trimmed);
  return trimmed.length > 0 && length !== null && length <= 200;
};
// Conservative UX checks only. The backend single-address parser is authoritative;
// do not duplicate it with an HTML/simple-email regex that rejects quoted local parts.
// Identity owns NFC/case normalization and uniqueness; preserve internal characters.
export const normalizeEmail = (value: string): string | null => {
  const trimmed = trimAccountWhitespace(value);
  const separator = trimmed.lastIndexOf('@');
  return trimmed.length <= 256 && scalarLength(trimmed) !== null &&
    !/[\u0000-\u001f\u007f-\u009f]/.test(trimmed) &&
    separator > 0 && separator < trimmed.length - 1 ? trimmed : null;
};
