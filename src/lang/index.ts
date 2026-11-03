// Language registry. Adding a language = a module here + an entry in Language/UnitI18n (types.ts)
// + its forms in data/units.json and data/units.schema.json.
import type { Language, LanguageInfo } from "../types.ts";
import { arabic } from "./ar.ts";
import { english } from "./en.ts";
import { french } from "./fr.ts";
import type { LanguageModule } from "./types.ts";

export const LANGUAGE_MODULES: Readonly<Record<Language, LanguageModule<any>>> = Object.freeze({
  ar: arabic,
  en: english,
  fr: french,
});

/** Supported output languages, in picker order (Arabic first, the default). */
export const LANGUAGES: readonly LanguageInfo[] = Object.freeze(
  Object.values(LANGUAGE_MODULES).map((m) => Object.freeze({ ...m.info })),
);

export function isLanguage(v: unknown): v is Language {
  return typeof v === "string" && Object.prototype.hasOwnProperty.call(LANGUAGE_MODULES, v);
}

export type { LanguageModule } from "./types.ts";
