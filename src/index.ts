// Public API. Zero dependencies; runs in browsers and Node.
import { Catalog } from "./catalog.ts";
import { TafqitError } from "./errors.ts";
import { phrase } from "./engine.ts";
import { normalizeNumber } from "./normalize.ts";
import defaultCatalogData from "./generated/units-data.ts";
import type { HundredsForm, TafqitOptions, TafqitResult, Unit, UnitCatalog } from "./types.ts";

export type * from "./types.ts";
export { TafqitError, ERROR_MESSAGES } from "./errors.ts";
export { normalizeNumber } from "./normalize.ts";
export { Catalog } from "./catalog.ts";

const HUNDREDS_FORMS: readonly HundredsForm[] = ["مائة", "مئة"];

export interface Tafqit {
  /** Convert a number to Arabic words. Throws TafqitError. */
  tafqit(input: string | number | bigint, options?: TafqitOptions): string;
  /** Same as tafqit, but never throws. */
  tryTafqit(input: string | number | bigint, options?: TafqitOptions): TafqitResult;
  /** Compatibility layer with field names the_number / unit / hundreds_form. */
  convert(req: { the_number: string | number; unit?: string; hundreds_form?: string }): { result: string };
  readonly units: readonly Unit[];
  findUnit(name: string): Unit | undefined;
}

function resolveHundreds(v: string | undefined): HundredsForm {
  if (v === undefined || v === "") return "مائة";
  if ((HUNDREDS_FORMS as readonly string[]).includes(v)) return v as HundredsForm;
  throw new TafqitError("INVALID_OPTION", `hundredsForm=${v}`);
}

/** Create an engine bound to a units catalog (e.g. a customised units.json). */
export function createTafqit(catalogData: UnitCatalog = defaultCatalogData): Tafqit {
  const catalog = new Catalog(catalogData);

  const tafqit = (input: string | number | bigint, options: TafqitOptions = {}): string => {
    const unit = catalog.get(options.unit);
    const hundredsForm = resolveHundreds(options.hundredsForm);
    const number = normalizeNumber(input);
    const text = phrase({ number, unit, hundredsForm });
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
    result: tafqit(req.the_number, { unit: req.unit, hundredsForm: req.hundreds_form as HundredsForm }),
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
