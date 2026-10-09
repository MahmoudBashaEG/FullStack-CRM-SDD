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
  const trimmed = value.trim(), length = scalarLength(trimmed);
  return trimmed.length > 0 && length !== null && length <= 200;
};
// Validation/trim only: the backend Identity normalizer owns canonical email uniqueness.
export const normalizeEmail = (value: string): string | null => {
  const trimmed = value.trim();
  return trimmed.length <= 256 && !/[\u0000-\u001f\u007f]/.test(trimmed) && /^[^\s<>@]+@[^\s<>@]+$/.test(trimmed) ? trimmed : null;
};
