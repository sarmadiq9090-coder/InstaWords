// Public API. Zero dependencies; runs in browsers and Node.
import { Catalog } from "./catalog.ts";
import { TafqitError } from "./errors.ts";
import { phrase } from "./engine.ts";
import { normalizeNumber } from "./normalize.ts";
import { isLanguage } from "./lang/index.ts";
import defaultCatalogData from "./generated/units-data.ts";
import type { HundredsForm, Language, TafqitOptions, TafqitResult, Unit, UnitCatalog } from "./types.ts";

export type * from "./types.ts";
export { TafqitError, ERROR_MESSAGES } from "./errors.ts";
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

  const tafqit = (input: string | number | bigint, options: TafqitOptions = {}): string => {
    const language = resolveLanguage(options.language);
    const unit = catalog.get(options.unit);
    // Validated for every language, but only Arabic uses it.
    const hundredsForm = resolveHundreds(options.hundredsForm);
    const number = normalizeNumber(input);
    const decimals = options.decimals ?? "round";
    if (decimals !== "round" && decimals !== "error") throw new TafqitError("INVALID_OPTION", `decimals=${decimals}`);
    const text = phrase({ number, unit, hundredsForm, decimals, language });
    return [options.prefix?.trim(), text, options.suffix?.trim()].filter(Boolean).join(" ");
  };

  const tryTafqit = (input: string | number | bigint, options?: TafqitOptions): TafqitResult => {
    let cleaned: string | undefined;
    try {
      cleaned = normalizeNumber(input).cleaned;
    } catch {
      /* reported below */
    }
    try {
      return { ok: true, result: tafqit(input, options), cleaned: cleaned! };
    } catch (e) {
      if (e instanceof TafqitError) {
        return { ok: false, error: { code: e.code, message: e.message }, ...(cleaned !== undefined ? { cleaned } : {}) };
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
