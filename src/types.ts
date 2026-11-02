export type Gender = "m" | "f";

export type HundredsForm = "مائة" | "مئة";

export interface NounForms {
  singular: string;
  dual: string;
  plural: string;
  accusative: string;
  gender: Gender;
}

export interface SubunitForms extends NounForms {
  ratio: number;
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
  | "INVALID_CATALOG";

export type TafqitResult =
  | { ok: true; result: string; cleaned: string }
  | { ok: false; error: { code: TafqitErrorCode; message: string }; cleaned?: string };
