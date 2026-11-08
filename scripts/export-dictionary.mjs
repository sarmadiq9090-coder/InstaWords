// Exports the shared FastKiwi Arabic number-word dictionary («قاموس عربي موحّد») as JSON, for other
// FastKiwi apps to copy at build time. The copy in this repo is docs/arabic-numbers.json (spec and copy
// instructions: docs/ARABIC-NUMBERS.md; tests/dictionary.test.ts fails when it is stale). The same bytes are
// published at https://fastkiwi.app/data/arabic-numbers.json (site repo data/arabic-numbers.json).
//
// Nothing here is typed by hand except the labels and the owner's digit rule (DIGIT_DISPLAY):
//   src/lexicon.ts           every Arabic word, the conjunction «و» and «فاصلة»
//   src/normalize.ts         the digit sets and separators, found by running normalizeNumber itself
//   tests/golden/cases.json  the default hundreds spelling and the Arabic examples
// source.commit is the last commit that changed one of those files, so re-running the export on a
// later commit gives the same bytes until the words really change.
//
// Usage:
//   node scripts/export-dictionary.mjs --out docs/arabic-numbers.json     write the file
//   node scripts/export-dictionary.mjs --check docs/arabic-numbers.json   exit 1 if it is stale
//   node scripts/export-dictionary.mjs                                         print to stdout
// Bump DICTIONARY_VERSION (semver) whenever the output changes: patch for a corrected word, minor for
// an added field, major for a changed or removed field.
import { execFileSync } from "node:child_process";
import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import * as LX from "../src/lexicon.ts";
import { normalizeNumber } from "../src/normalize.ts";

export const DICTIONARY_VERSION = "1.0.0";
export const SOURCE_FILES = ["src/lexicon.ts", "src/normalize.ts", "tests/golden/cases.json"];

const root = fileURLToPath(new URL("..", import.meta.url));
const SCALE_IDS = [null, "thousand", "million", "billion", "trillion"];
const DIGIT_SETS = [
  ["arabic-indic", 0x0660], // ٠–٩ U+0660–U+0669
  ["persian", 0x06f0], // ۰–۹ U+06F0–U+06F9 (Extended Arabic-Indic)
];

/** Owner decision D-012 (fastkiwi-ai-brain core/DECISIONS.md, 2026-11-06). */
export const DIGIT_DISPLAY = {
  default: "latin",
  options: ["latin", "arabic-indic"],
  decision: "D-012",
  note: {
    ar: "تعرض تطبيقات FastKiwi الأرقام بالأرقام الغربية 123 افتراضياً، والأرقام العربية الهندية ١٢٣ خيار يختاره المستخدم فقط ولا تُفرض عليه. تطبيق «ذكّرني» يعرض 123 فقط بلا خيار. وينبغي أن يقبل كل حقل إدخال الأرقام الغربية والعربية الهندية والفارسية والفواصل المذكورة في digits.",
    en: "FastKiwi apps display numbers with Western digits (123) by default; Arabic-Indic digits (١٢٣) are only a user option, never forced. Remember Me shows 123 only, with no option. Every input field should accept Western, Arabic-Indic and Persian digits and the separators listed under digits.",
  },
};

const fromList = (list, from, to, step = 1) => {
  const out = {};
  for (let i = from; i <= to; i++) out[String(i * step)] = list[i];
  return out;
};
const byGender = (table, from, to) => ({ m: fromList(table.m, from, to), f: fromList(table.f, from, to) });

/** Digits and separators accepted by InstaWords' normalizeNumber, found by trying every BMP character. */
function digitTables() {
  const toLatin = Object.fromEntries(DIGIT_SETS.map(([id]) => [id, {}]));
  const thousands = [], decimal = [], whitespace = [];
  const tryNormalize = (s) => { try { return normalizeNumber(s).cleaned; } catch { return null; } };
  for (let cp = 0; cp <= 0xffff; cp++) {
    if (cp >= 0xd800 && cp <= 0xdfff) continue;
    const ch = String.fromCharCode(cp);
    if (ch >= "0" && ch <= "9") continue;
    const alone = tryNormalize(ch);
    if (alone !== null && /^\d$/.test(alone)) {
      const set = DIGIT_SETS.find(([, base]) => cp >= base && cp <= base + 9);
      if (!set || cp - set[1] !== Number(alone)) throw new Error(`unexpected digit U+${cp.toString(16)}: update DIGIT_SETS`);
      toLatin[set[0]][ch] = alone;
      continue;
    }
    const between = tryNormalize("1" + ch + "5");
    if (between === "15") (/\s/u.test(ch) ? whitespace : thousands).push(ch);
    else if (between === "1.5") decimal.push(ch);
  }
  for (const [id] of DIGIT_SETS) if (Object.keys(toLatin[id]).length !== 10) throw new Error(`${id}: not 10 digits`);
  if (!decimal.includes(".")) throw new Error("'.' is not a decimal point");
  return {
    toLatin,
    separators: { thousands, decimal },
    ignored: whitespace.map((ch) => "U+" + ch.charCodeAt(0).toString(16).toUpperCase().padStart(4, "0")),
  };
}

/** The last commit that changed a source file (stable across commits that leave the words alone). */
export function sourceCommit() {
  return execFileSync("git", ["log", "-1", "--format=%H", "--", ...SOURCE_FILES], { cwd: root, encoding: "utf8" }).trim();
}

export function buildDictionary({ commit = sourceCommit() } = {}) {
  const golden = JSON.parse(readFileSync(root + "tests/golden/cases.json", "utf8"));
  const hundreds = {};
  for (const [form, table] of Object.entries(LX.HUNDREDS)) {
    hundreds[form] = { words: fromList(table.words, 1, 9, 100), dualConstruct: table.dualConstruct };
  }
  const scales = {};
  LX.SCALES.forEach((s, k) => {
    if (s) scales[SCALE_IDS[k]] = { exponent: 3 * k, singular: s.singular, dual: s.dual, plural: s.plural, accusative: s.accusative };
  });
  const teens = (g) => Object.fromEntries(LX.TEENS[g].slice(1).map((w, i) => [String(11 + i), w]));
  const examples = golden.cases
    .filter((c) => (c.options?.language ?? "ar") === "ar")
    .map((c) => {
      const { language, ...options } = c.options ?? {};
      return { id: c.id, number: c.input, cleaned: c.cleaned, unit: c.unit, ...(Object.keys(options).length ? { options } : {}), expected: c.expected };
    });
  return {
    $schema: "./arabic-numbers.schema.json",
    schemaVersion: 1,
    version: DICTIONARY_VERSION,
    title: { ar: "قاموس عربي موحّد لألفاظ الأعداد", en: "FastKiwi unified Arabic number-word dictionary" },
    description: "Data only, no grammar engine: the number words, digits and separators that every FastKiwi app shares. Generated from InstaWords; do not edit by hand. Grammar rules: docs/SPEC.md in InstaWords.",
    source: {
      repository: "https://github.com/sarmadiq9090-coder/InstaWords",
      commit,
      files: SOURCE_FILES,
      spec: "docs/SPEC.md",
      generator: "scripts/export-dictionary.mjs",
    },
    words: {
      zero: LX.ZERO,
      ones: byGender(LX.ONES, 1, 10),
      teens: { m: teens("m"), f: teens("f") },
      compoundOnes: byGender(LX.COMPOUND_ONES, 1, 9),
      tens: fromList(LX.TENS, 2, 9, 10),
      hundreds: { default: golden.defaults.hundredsForm, forms: hundreds },
      scales,
      decimalPoint: LX.DECIMAL_POINT_WORD,
    },
    conjunction: { word: LX.WAW.trim(), joiner: LX.WAW },
    digits: digitTables(),
    digitDisplay: DIGIT_DISPLAY,
    examples,
  };
}

export const serialize = (dict) => JSON.stringify(dict, null, 2) + "\n";

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const arg = (name) => { const i = process.argv.indexOf(name); return i < 0 ? null : process.argv[i + 1]; };
  const text = serialize(buildDictionary());
  const check = arg("--check"), out = arg("--out");
  if (check) {
    let current = null;
    try { current = readFileSync(check, "utf8"); } catch {}
    if (current !== text) {
      console.error(`${check} is not the export of this commit: run node scripts/export-dictionary.mjs --out ${check}`);
      process.exit(1);
    }
    console.log(`${check} is up to date (version ${DICTIONARY_VERSION}).`);
  } else if (out) {
    writeFileSync(out, text);
    console.log(`${out}: version ${DICTIONARY_VERSION}, ${text.length} characters`);
  } else process.stdout.write(text);
}
