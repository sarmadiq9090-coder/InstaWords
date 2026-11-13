// Public API. Zero dependencies; runs in browsers and Node.
import { Catalog } from "./catalog.ts";
import { TafqitError, WARNING_MESSAGES } from "./errors.ts";
import { phrase } from "./engine.ts";
import { normalizeNumber } from "./normalize.ts";
import { isLanguage } from "./lang/index.ts";
import defaultCatalogData from "./generated/units-data.ts";
import type { HundredsForm, Language, TafqitOptions, TafqitResult, TafqitWarningCode, Unit, UnitCatalog } from "./types.ts";

export type * from "./types.ts";
export { TafqitError, ERROR_MESSAGES, WARNING_MESSAGES } from "./errors.ts";
export { normalizeNumber } from "./normalize.ts";
export { Catalog } from "./catalog.ts";
export { LANGUAGES } from "./lang/index.ts";

const HUNDREDS_FORMS: readonly HundredsForm[] = ["مائة", "مئة"];

export interface Tafqit {
  /** Convert a number to words (Arabic by default, see options.language). Throws TafqitError. */
  tafqit(input: string | number | bigint, options?: TafqitOptions): string;
  /** Same as tafqit, but never throws. */
  tryTafqit(input: string | number | bigint, options?: TafqitOptions): TafqitResult;
  /** Compatibility layer with field names the_number / unit / hundreds_form. */
  convert(req: { the_number: string | number; unit?: string; hundreds_form?: string; language?: Language }): { result: string };
  readonly units: readonly Unit[];
  findUnit(name: string): Unit | undefined;
}

function resolveHundreds(v: string | undefined): HundredsForm {
  if (v === undefined || v === "") return "مائة";
  if ((HUNDREDS_FORMS as readonly string[]).includes(v)) return v as HundredsForm;
  throw new TafqitError("INVALID_OPTION", `hundredsForm=${v}`);
}

function resolveLanguage(v: string | undefined): Language {
  if (v === undefined || v === "") return "ar";
  if (isLanguage(v)) return v;
  throw new TafqitError("INVALID_OPTION", `language=${v}`);
}

/** Create an engine bound to a units catalog (e.g. a customised units.json). */
export function createTafqit(catalogData: UnitCatalog = defaultCatalogData): Tafqit {
  const catalog = new Catalog(catalogData);

  const run = (input: string | number | bigint, options: TafqitOptions, warnings: TafqitWarningCode[]): string => {
    const language = resolveLanguage(options.language);
    const unit = catalog.get(options.unit);
    // Validated for every language, but only Arabic uses it.
    const hundredsForm = resolveHundreds(options.hundredsForm);
    const number = normalizeNumber(input, language);
    const decimals = options.decimals ?? "round";
    if (decimals !== "round" && decimals !== "error") throw new TafqitError("INVALID_OPTION", `decimals=${decimals}`);
    const text = phrase({ number, unit, hundredsForm, decimals, language, warnings });
    return [options.prefix?.trim(), text, options.suffix?.trim()].filter(Boolean).join(" ");
  };

  const tafqit = (input: string | number | bigint, options: TafqitOptions = {}): string => run(input, options, []);

  const tryTafqit = (input: string | number | bigint, options: TafqitOptions = {}): TafqitResult => {
    let cleaned: string | undefined;
    try {
      // The separator convention of the output language; Arabic's when the language is invalid.
      cleaned = normalizeNumber(input, isLanguage(options.language) ? options.language : "ar").cleaned;
    } catch {
      /* reported below */
    }
    try {
      const warnings: TafqitWarningCode[] = [];
      const result = run(input, options, warnings);
      return {
        ok: true, result, cleaned: cleaned!,
        ...(warnings.length ? { warnings: warnings.map((code) => ({ code, message: WARNING_MESSAGES[code] })) } : {}),
      };
    } catch (e) {
      if (e instanceof TafqitError) {
        const error = { code: e.code, message: e.message, ...(e.readings ? { readings: [...e.readings] } : {}) };
        return { ok: false, error, ...(cleaned !== undefined ? { cleaned } : {}) };
      }
      throw e;
    }
  };

  const convert: Tafqit["convert"] = (req) => ({
    result: tafqit(req.the_number, { unit: req.unit, hundredsForm: req.hundreds_form as HundredsForm, language: req.language }),
  });

  return { tafqit, tryTafqit, convert, units: catalog.units, findUnit: (n) => catalog.find(n) };
}

const defaultEngine = createTafqit();

export const tafqit = defaultEngine.tafqit;
export const tryTafqit = defaultEngine.tryTafqit;
export const convert = defaultEngine.convert;
export const units = defaultEngine.units;
export const findUnit = defaultEngine.findUnit;
export default tafqit;
