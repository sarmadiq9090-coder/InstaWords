// Unit catalog loading and lookup, docs/SPEC.md §14.
import { TafqitError } from "./errors.ts";
import type { NounForms, Unit, UnitCatalog } from "./types.ts";

const FORM_KEYS = ["singular", "dual", "plural", "accusative"] as const;

function checkForms(f: NounForms, where: string): void {
  for (const k of FORM_KEYS) {
    const v = f[k];
    if (typeof v !== "string" || v.trim() === "" || v !== v.trim()) {
      throw new TafqitError("INVALID_CATALOG", `${where}.${k}`);
    }
  }
  if (f.gender !== "m" && f.gender !== "f") throw new TafqitError("INVALID_CATALOG", `${where}.gender`);
}

function isPowerOfTen(n: number): boolean {
  if (!Number.isInteger(n) || n < 10) return false;
  while (n % 10 === 0) n /= 10;
  return n === 1;
}

/** Lookup key: trimmed, collapsed spaces, Latin lower-cased. */
export function unitKey(name: string): string {
  return name.trim().replace(/\s+/g, " ").toLowerCase();
}

export class Catalog {
  readonly units: readonly Unit[];
  private readonly index = new Map<string, Unit>();

  constructor(data: UnitCatalog) {
    if (!data || data.schemaVersion !== 1 || !Array.isArray(data.units)) {
      throw new TafqitError("INVALID_CATALOG", "schemaVersion/units");
    }
    for (const u of data.units) this.add(u);
    this.units = data.units;
  }

  private add(u: Unit): void {
    const where = `units[${u?.id}]`;
    if (!u || typeof u.id !== "string" || !/^[A-Za-z][A-Za-z0-9_-]*$/.test(u.id)) {
      throw new TafqitError("INVALID_CATALOG", `${where}.id`);
    }
    if (typeof u.label !== "string" || u.label.trim() === "") throw new TafqitError("INVALID_CATALOG", `${where}.label`);
    if (u.forms === null) {
      if (u.category !== "none" || u.subunit) throw new TafqitError("INVALID_CATALOG", `${where}.forms`);
    } else {
      checkForms(u.forms, `${where}.forms`);
    }
    if (u.subunit) {
      checkForms(u.subunit, `${where}.subunit`);
      if (!isPowerOfTen(u.subunit.ratio) || u.subunit.ratio > 1e8) {
        throw new TafqitError("INVALID_CATALOG", `${where}.subunit.ratio`);
      }
    }
    // id must be unique; other names may repeat only within the same unit.
    const idKey = unitKey(u.id);
    if (this.index.has(idKey)) throw new TafqitError("INVALID_CATALOG", `duplicate name "${u.id}"`);
    this.index.set(idKey, u);
    const names = new Set([u.label, u.code ?? "", ...(u.aliases ?? [])].map(unitKey).filter(Boolean));
    names.delete(idKey);
    for (const k of names) {
      const other = this.index.get(k);
      if (other && other !== u) throw new TafqitError("INVALID_CATALOG", `duplicate name "${k}"`);
      this.index.set(k, u);
    }
  }

  find(name: string): Unit | undefined {
    return this.index.get(unitKey(name));
  }

  get(name: string | undefined): Unit {
    const u = this.find(name === undefined || name === "" ? "none" : name);
    if (!u) throw new TafqitError("UNKNOWN_UNIT", name);
    return u;
  }
}
