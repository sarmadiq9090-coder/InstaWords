// Common interface of the per-language modules, docs/SPEC.md §17.
import type { HundredsForm, LanguageInfo, NounForms, UnitI18n } from "../types.ts";

/**
 * One output language. Normalisation, rounding, limits and errors are shared (engine.ts);
 * a module only turns digit strings into words.
 */
export interface LanguageModule<F = unknown> {
  readonly info: LanguageInfo;
  /**
   * Catalog validation of this language's entry in `i18n` (absent for Arabic, whose forms
   * are the unit's own fields): `gender` says whether the forms carry a required gender.
   */
  readonly i18nForms?: { gender: boolean };
  /** Forms of a unit or subunit in this language, or undefined when the catalog has none. */
  pickForms(arabic: NounForms, i18n: UnitI18n | undefined): F | undefined;
  /** A number with no counted noun. `fraction`: digits after the point, "" when none. */
  plain(integer: string, fraction: string, hf: HundredsForm): string;
  /** A number (digits, no leading zeros) followed by its counted noun. */
  count(digits: string, forms: F, hf: HundredsForm): string;
  /** Placed between the unit phrase and the subunit phrase. */
  readonly subunitJoiner: string;
}

/** Split a digit string into 3-digit groups, least significant first. */
export function splitGroups(digits: string): number[] {
  const groups: number[] = [];
  for (let end = digits.length; end > 0; end -= 3) {
    groups.push(parseInt(digits.slice(Math.max(0, end - 3), end), 10));
  }
  return groups;
}
