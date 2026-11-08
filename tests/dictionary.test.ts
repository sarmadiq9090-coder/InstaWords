// scripts/export-dictionary.mjs: the shared dictionary is the lexicon, word for word.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
// @ts-ignore: plain ES module script, no type declarations
import { buildDictionary, serialize, DICTIONARY_VERSION } from "../scripts/export-dictionary.mjs";
import * as LX from "../src/lexicon.ts";
import { normalizeNumber } from "../src/normalize.ts";

const dict = buildDictionary({ commit: "0".repeat(40) });
const golden = JSON.parse(readFileSync(new URL("./golden/cases.json", import.meta.url), "utf8"));

test("dictionary: header", () => {
  assert.match(DICTIONARY_VERSION, /^\d+\.\d+\.\d+$/);
  assert.equal(dict.version, DICTIONARY_VERSION);
  assert.equal(dict.schemaVersion, 1);
  assert.equal(dict.source.commit, "0".repeat(40));
});

test("dictionary: every word comes from src/lexicon.ts", () => {
  const w = dict.words;
  assert.equal(w.zero, LX.ZERO);
  assert.equal(w.decimalPoint, LX.DECIMAL_POINT_WORD);
  for (const g of ["m", "f"] as const) {
    for (let n = 1; n <= 10; n++) assert.equal(w.ones[g][n], LX.ONES[g][n]);
    for (let n = 11; n <= 19; n++) assert.equal(w.teens[g][n], LX.TEENS[g][n - 10]);
    for (let n = 1; n <= 9; n++) assert.equal(w.compoundOnes[g][n], LX.COMPOUND_ONES[g][n]);
    assert.equal(Object.keys(w.ones[g]).length, 10);
    assert.equal(Object.keys(w.teens[g]).length, 9);
  }
  for (let t = 2; t <= 9; t++) assert.equal(w.tens[t * 10], LX.TENS[t]);
  assert.deepEqual(Object.keys(w.hundreds.forms), Object.keys(LX.HUNDREDS));
  assert.equal(w.hundreds.default, golden.defaults.hundredsForm);
  for (const [form, table] of Object.entries(LX.HUNDREDS)) {
    for (let h = 1; h <= 9; h++) assert.equal(w.hundreds.forms[form].words[h * 100], table.words[h]);
    assert.equal(w.hundreds.forms[form].dualConstruct, table.dualConstruct);
  }
  assert.deepEqual(Object.keys(w.scales), ["thousand", "million", "billion", "trillion"]);
  Object.values(w.scales).forEach((s: any, i) => assert.deepEqual(s, { exponent: 3 * (i + 1), ...LX.SCALES[i + 1] }));
  assert.deepEqual(dict.conjunction, { word: "و", joiner: LX.WAW });
});

test("dictionary: digits and separators are what normalizeNumber accepts", () => {
  const { toLatin, separators } = dict.digits;
  assert.deepEqual(Object.keys(toLatin), ["arabic-indic", "persian"]);
  for (const [set, base] of [["arabic-indic", 0x0660], ["persian", 0x06f0]] as const) {
    for (let d = 0; d <= 9; d++) assert.equal(toLatin[set][String.fromCharCode(base + d)], String(d));
  }
  assert.deepEqual(separators, { thousands: [",", "٬"], decimal: [".", "٫"] });
  for (const s of separators.thousands) assert.equal(normalizeNumber(`1${s}000`).cleaned, "1000");
  for (const s of separators.decimal) assert.equal(normalizeNumber(`1${s}5`).cleaned, "1.5");
  assert.ok(dict.digits.ignored.includes("U+00A0"));
  assert.deepEqual(dict.digitDisplay.options, ["latin", "arabic-indic"]);
  assert.equal(dict.digitDisplay.default, "latin");
});

test("dictionary: examples are the Arabic golden cases, unchanged", () => {
  const arabic = golden.cases.filter((c: any) => (c.options?.language ?? "ar") === "ar");
  assert.equal(dict.examples.length, arabic.length);
  dict.examples.forEach((e: any, i: number) => {
    const c = arabic[i];
    assert.deepEqual([e.id, e.number, e.cleaned, e.unit, e.expected], [c.id, c.input, c.cleaned, c.unit, c.expected]);
    assert.equal(e.options?.language, undefined);
  });
});

test("dictionary: the export is reproducible and has no empty word", () => {
  const text = serialize(dict);
  assert.equal(serialize(buildDictionary({ commit: "0".repeat(40) })), text);
  assert.ok(!text.includes('""'));
});

test("dictionary: docs/arabic-numbers.json is the export and matches its schema", async () => {
  // @ts-ignore: plain ES module, no type declarations
  const { validate } = await import("./lib/json-schema.mjs");
  const read = (name: string) => readFileSync(new URL(`../docs/${name}`, import.meta.url), "utf8");
  const text = read("arabic-numbers.json");
  const copy = JSON.parse(text);
  // source.commit is compared by format only: a shallow CI checkout cannot see the real last commit.
  assert.match(copy.source.commit, /^[0-9a-f]{40}$/);
  assert.equal(text, serialize(buildDictionary({ commit: copy.source.commit })),
    "docs/arabic-numbers.json is stale: run npm run export:dictionary -- --out docs/arabic-numbers.json");
  assert.deepEqual(validate(JSON.parse(read("arabic-numbers.schema.json")), copy), []);
});
