export type Gender = "m" | "f";

/** Output language (docs/SPEC.md §17). */
export type Language = "ar" | "en" | "fr";

/** Entry of LANGUAGES: everything a UI needs to build a language picker. */
export interface LanguageInfo {
  id: Language;
  /** Native name: العربية, English, Français. */
  label: string;
  dir: "rtl" | "ltr";
  /** Whether the hundredsForm option changes the output. */
  supportsHundredsForm: boolean;
}

export type HundredsForm = "مائة" | "مئة";

/** What to do when a fraction has more digits than the subunit allows. */
export type DecimalsPolicy = "round" | "error";

export interface NounForms {
  singular: string;
  dual: string;
  plural: string;
  accusative: string;
  gender: Gender;
  /** Optional phrase for exactly 1 (default: singular + واحد/واحدة). */
  one?: string;
}

/** English forms of a counted noun (Title Case). */
export interface EnglishForms {
  /** Used with 1: Saudi Riyal. */
  singular: string;
  /** Used with every other number, including 0: Saudi Riyals. */
  plural: string;
  /** Optional phrase for exactly 1 (default: "One " + singular). */
  one?: string;
}

/** French forms of a counted noun (lowercase). */
export interface FrenchForms {
  /** Used with 0 and 1: riyal saoudien. */
  singular: string;
  /** Used with 2 and more: riyals saoudiens. */
  plural: string;
  /** Gender of the noun: un/une, vingt et un/vingt et une. */
  gender: Gender;
  /** Optional phrase for exactly 1 (default: "un"/"une" + " " + singular). */
  one?: string;
}

/** Forms in languages other than Arabic. A missing language → NO_LANGUAGE_FORMS. */
export interface UnitI18n {
  en?: EnglishForms;
  fr?: FrenchForms;
}

export interface SubunitForms extends NounForms {
  ratio: number;
  i18n?: UnitI18n;
}

export type UnitCategory =
  | "currency-arab"
  | "currency-world"
  | "crypto"
  | "generic-currency"
  | "weight"
  | "volume"
  | "length"
  | "area"
  | "time"
  | "count"
  | "data"
  | "speed"
  | "none";

export interface Unit {
  id: string;
  category: UnitCategory;
  label: string;
  code?: string;
  aliases?: string[];
  forms: NounForms | null;
  subunit?: SubunitForms;
  /** Forms of the unit in other languages (the subunit carries its own). */
  i18n?: UnitI18n;
  /**
   * Display only, never the words or the rounding: decimals an app shows for a whole amount of
   * this money, e.g. 0 for IQD (1,750,000, not 1,750,000.000). Absent: the subunit's digits.
   * An amount with a fraction shows every subunit digit. Needs a subunit; at most its digits.
   */
  displayDigits?: number;
  note?: string;
}

export interface UnitCatalog {
  $schema?: string;
  schemaVersion: 1;
  units: Unit[];
}

export interface TafqitOptions {
  /** Unit id, ISO code, label or alias. Default: "none". */
  unit?: string;
  /** Default: "مائة". */
  hundredsForm?: HundredsForm;
  prefix?: string;
  suffix?: string;
  /** Extra fraction digits: "round" half-up to the subunit (default) or "error". */
  decimals?: DecimalsPolicy;
  /** Output language. Default: "ar". hundredsForm is ignored for "en" and "fr". */
  language?: Language;
}

export interface NormalizedNumber {
  /** Integer part, digits only, no leading zeros ("0" for zero). */
  integer: string;
  /** Fraction digits with trailing zeros removed ("" when none). */
  fraction: string;
  /** Canonical display form: integer[.fraction]. */
  cleaned: string;
}

export type TafqitErrorCode =
  | "EMPTY"
  | "INVALID_FORMAT"
  | "NEGATIVE"
  | "TOO_LARGE"
  | "TOO_MANY_DECIMALS"
  | "FRACTION_NOT_SUPPORTED"
  | "UNKNOWN_UNIT"
  | "INVALID_OPTION"
  | "INVALID_CATALOG"
  | "NO_LANGUAGE_FORMS";

export type TafqitResult =
  | { ok: true; result: string; cleaned: string }
  | { ok: false; error: { code: TafqitErrorCode; message: string }; cleaned?: string };
