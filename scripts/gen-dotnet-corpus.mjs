// Writes the parity corpus of the C# port: inputs × units × languages × options, with the
// expected output of the TypeScript core (dist/index.js). Read by
// windows/InstaWords.Core.Tests/ParityTests.cs, which asserts 100% equality.
//
//   npm run build && node scripts/gen-dotnet-corpus.mjs [--out <file>]
//
// Default output: windows/InstaWords.Core.Tests/corpus/parity.tsv (gitignored).
//
// Format: UTF-8, one record per line, fields separated by TAB. "~" is an absent value
// (undefined / null); any other field is a string in which "\", "~", TAB, controls,
// surrogates and other invisible characters are written \uXXXX (and "\" as \\).
//   # <comment>                                     header lines
//   T cat input unit hf prefix suffix decimals lang ok out message cleaned warnings readings
//       cat 0 = bundled catalog, 1 = CUSTOM catalog (see customCatalog below);
//       ok 1: out = words; ok 0: out = error code, message = e.message (with detail)
//       warnings: tryTafqit warning codes joined by "," (~ when none)
//       readings: error.readings joined by "|" (~ when absent)
//   C input lang ok out                             normalizeNumber(input, lang): ok 1 → cleaned, 0 → error code
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { createTafqit, normalizeNumber, tafqit, tryTafqit, units } from "../dist/index.js";
import { buildDotnet } from "./build-dotnet.mjs";

const root = fileURLToPath(new URL("..", import.meta.url));
const argv = process.argv.slice(2);
const outIdx = argv.indexOf("--out");
const outPath = outIdx === -1 ? root + "windows/InstaWords.Core.Tests/corpus/parity.tsv" : resolve(argv[outIdx + 1]);

const HF = ["مائة", "مئة"];
const LANGS = ["ar", "en", "fr"];

// ------------------------------------------------------------------ custom catalog

/**
 * Same edits as tests/vba/parity.test.mjs, mirrored in ParityTests.CustomCatalog():
 * missing language forms (NO_LANGUAGE_FORMS), extra "one" forms, a flipped French gender.
 */
function customCatalog() {
  const data = JSON.parse(readFileSync(root + "data/units.json", "utf8"));
  const by = (id) => data.units.find((u) => u.id === id);
  delete by("SAR").subunit.i18n.fr; // subunit only: 2.00 works, 2.50 fails
  delete by("KWD").i18n.en;
  delete by("riyal").i18n;
  delete by("BTC").subunit.i18n;
  by("EUR").i18n.en.one = "One Single Euro";
  by("EUR").subunit.i18n.fr.one = "un seul centime";
  by("lira").i18n.fr.gender = by("lira").i18n.fr.gender === "f" ? "m" : "f";
  by("riyal").forms.one = "ريال فقط";
  return data;
}
const custom = createTafqit(customCatalog());

// ------------------------------------------------------------------ random

function rng(seed) {
  return () => {
    seed = (seed + 0x6d2b79f5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
const rand = rng(20261103);
const int = (n) => Math.floor(rand() * n);
const pick = (a) => a[int(a.length)];
const digits = (n, lead = true) => {
  let s = lead ? String(1 + int(9)) : String(int(10));
  while (s.length < n) s += String(int(10));
  return s;
};

const unitIds = units.map((u) => u.id);
const withSub = units.filter((u) => u.subunit).map((u) => u.id);
const withoutSub = units.filter((u) => u.forms && !u.subunit).map((u) => u.id);

// ------------------------------------------------------------------ corpus

function buildCorpus() {
  const C = [];
  const add = (c) => C.push(c);

  // every integer 0..2000 in every language with: no unit, masculine, feminine, unit with subunit
  for (let n = 0; n <= 2000; n++) {
    for (const lang of LANGS) for (const unit of ["none", "riyal", "lira", "SAR"]) add({ input: String(n), unit, lang });
    add({ input: String(n), unit: pick(unitIds), hf: HF[1] });
  }
  // numbers built from the groups where the rules change (French 80/cent/mille, Arabic 2/3-10/11+)
  const GROUPS = [0, 0, 0, 1, 2, 3, 7, 10, 11, 12, 19, 20, 21, 22, 30, 61, 70, 71, 77, 80, 81, 89, 90, 91, 99, 100, 101,
    102, 103, 110, 111, 180, 200, 201, 202, 211, 280, 300, 381, 999];
  for (let i = 0; i < 20000; i++) {
    const n = 1 + int(5);
    let s = String(pick(GROUPS.filter(Boolean)));
    for (let k = 1; k < n; k++) s += String(pick(GROUPS)).padStart(3, "0");
    add({ input: s, unit: i % 6 === 0 ? "none" : pick(unitIds), hf: pick(["", ...HF]), lang: LANGS[i % 3] });
  }
  // random 1..15 digit integers (and some with zero groups)
  for (let i = 0; i < 8000; i++) {
    let s = digits(1 + int(15));
    if (i % 4 === 0) s = s.replace(/\d{3}(?=(\d{3})*$)/g, (g) => (rand() < 0.5 ? "000" : g)).replace(/^0+/, "") || "0";
    add({ input: s, unit: pick(unitIds), hf: pick(["", ...HF]), lang: pick(LANGS) });
  }
  // fractions
  for (let i = 0; i < 12000; i++) {
    const ip = rand() < 0.2 ? "0" : digits(1 + int(i % 10 === 0 ? 15 : 7));
    const fp = digits(1 + int(i % 7 === 0 ? 17 : 9), false);
    const unit = i % 5 === 0 ? "none" : i % 11 === 0 ? pick(withoutSub) : pick(withSub);
    add({ input: `${rand() < 0.1 ? "" : ip}.${fp}`, unit, hf: pick(["", ...HF]), decimals: pick([undefined, undefined, "round", "error"]), lang: pick(LANGS) });
  }
  // rounding carries and limits
  for (const unit of ["SAR", "KWD", "BTC", "meter", "none", "ETH", "USD"]) {
    for (const s of ["0.995", "0.9999", "9.995", "99.9951", "999.9995", "1.005", "1.0049", "0.0049", "0.005",
      "999999999999999.99", "999999999999999.995", "999999999999999.9999", "99999999999999.999", "0.00000000049",
      "0.000000005", "123.4500001", "5.", ".5", "0.999", "0.000000000000001", "0.0000000000000001", "1.999999999999999",
      "999999999999999.999999999999999", "0.0000000049", "0.00000001", "0.000000015", "0.9999999999"]) {
      for (const decimals of [undefined, "error"]) for (const lang of LANGS) add({ input: s, unit, decimals, lang });
    }
  }
  // input formats: Indic/Persian digits, separators, spaces, signs, zeros
  const indic = (s) => s.replace(/\d/g, (d) => String.fromCharCode(0x660 + Number(d)));
  const persian = (s) => s.replace(/\d/g, (d) => String.fromCharCode(0x6f0 + Number(d)));
  const group = (s, sep) => s.replace(/\B(?=(\d{3})+(?!\d))/g, sep);
  // removed by normalizeNumber, other JS whitespace (not removed), and lookalikes
  const spaces = [" ", " ", " ", " ", "\t", "\r", "\n", "　", " ", "﻿", "\u0085", "᠎",
    "​", " ", "\u000b", "\u000c", " ", " "];
  for (let i = 0; i < 8000; i++) {
    const ip = digits(1 + int(12));
    const fp = rand() < 0.5 ? "" : digits(1 + int(3), false);
    let s = rand() < 0.5 ? group(ip, pick([",", "٬", " ", " ", " ", " ", "'", "٬٬", "_"])) : ip;
    if (fp) s += pick([".", "٫", ".", "٫", ",", "⎖"]) + fp + (rand() < 0.3 ? "00" : "");
    if (rand() < 0.3) s = "00" + s;
    const f = int(4);
    if (f === 1) s = indic(s);
    if (f === 2) s = persian(s);
    if (f === 3) s = s.split("").map((ch) => (rand() < 0.5 ? indic(ch) : persian(ch))).join("");
    if (rand() < 0.3) s = pick(spaces) + s + pick(spaces);
    if (rand() < 0.1) s = pick(["+", "-", "−", "++", "+-", "–", "＋", "﹣"]) + s;
    add({ input: s, unit: pick(["none", "SAR", "KWD", "riyal", "lira", "piece", "BTC"]), lang: pick(LANGS) });
  }
  // every unit: numbers through all count forms, N=1 (incl. the "one" form), lookups by every name
  const nums = ["0", "1", "2", "3", "8", "10", "11", "19", "20", "21", "28", "99", "100", "101", "102", "103", "111",
    "200", "201", "1000", "1001", "2000", "2002", "3000", "11000", "100000", "101000", "102000", "200000", "1000000",
    "2000000", "11000000", "200000000", "1000000000", "2000000000000", "999999999999999", "71", "80", "81", "91",
    "0.1", "0.01", "0.02", "0.5", "1.01", "1.02", "2.03", "12.345", "2.5", "0.001", "1.000001", "3.11",
    "21000", "80000", "1000001", "80000000", "1000000.5", "2000000.01", "201", "1000000000000", "180", "280000",
    "300000000", "1000000.000001", "0.00000001", "1.5", "2.00", "21.21", "80.80", "1000000000.01"];
  for (const u of units) {
    for (const n of nums) {
      for (const hf of [undefined, HF[1]]) add({ input: n, unit: u.id, hf });
      add({ input: n, unit: u.id, lang: "en" });
      add({ input: n, unit: u.id, lang: "fr" });
      add({ input: n, unit: u.id, lang: "fr", decimals: "error" });
    }
    const names = [u.id, u.label, u.code, ...(u.aliases ?? [])].filter(Boolean);
    for (const name of names) {
      add({ input: "1", unit: name, lang: pick(LANGS) });
      add({ input: "15", unit: `  ${name.toUpperCase()} `, lang: pick(LANGS) });
      add({ input: "2", unit: name.replace(/ /g, " \t "), hf: HF[1], lang: pick(LANGS) });
      add({ input: "1", unit: name.toLowerCase(), lang: pick(LANGS) });
      add({ input: "3", unit: `${pick(spaces)}${name.replace(/ /g, () => pick(spaces))}${pick(spaces)}`, lang: pick(LANGS) });
      add({ input: "4", unit: name.replace(/k/gi, "K"), lang: pick(LANGS) });
      add({ input: "5", unit: name + "İ", lang: pick(LANGS) });
    }
    if (u.subunit) {
      for (const sub of ["0.1", "0.01", "1.1", "0." + "0".repeat(String(u.subunit.ratio).length - 2) + "1", "1000000.01"]) {
        for (const lang of LANGS) add({ input: sub, unit: u.id, lang });
      }
    }
  }
  // hundreds forms and options (passed to the core as-is: no lenient mapping)
  for (const n of ["100", "200", "300", "900", "200000", "125", "1250.75"]) {
    for (const hf of [undefined, "", "1", "2", ...HF, "x", " مئة", "مائه", "3", "مائة ", "MIA"]) for (const lang of LANGS) add({ input: n, unit: "SAR", hf, lang });
    for (const decimals of [undefined, "", "round", "error", "ROUND", " Error ", "x", "rounded", "Round"]) add({ input: n + ".125", unit: "SAR", decimals, lang: pick(LANGS) });
    for (const lang of [undefined, "", "ar", "en", "fr", "EN", " fr ", "Fr", " en\t", "de", "english", "e n", "ara", "Kn", "AR"]) {
      add({ input: n, unit: "SAR", lang });
      add({ input: n, unit: "nope", lang });
      add({ input: "abc", unit: "SAR", lang });
    }
  }
  // error precedence: several problems at once
  for (const input of ["", "abc", "-5", "1.555", "1.5", "1234567890123456", "5"]) {
    for (const unit of [undefined, "nope", "piece", "SAR"]) {
      for (const lang of [undefined, "xx"]) for (const hf of [undefined, "x"]) for (const decimals of [undefined, "error", "x"]) {
        add({ input, unit, lang, hf, decimals });
      }
    }
  }
  // prefix / suffix
  for (const [prefix, suffix] of [["فقط", "لا غير"], ["  فقط ", " لا غير  "], ["　فقط ", "\tلا غير\n"], ["", "لا غير"],
    ["فقط", ""], ["   ", " "], ["Only", "only."], ["﻿فقط", "لا  غير"], ["\u0085فقط\u0085", "᠎"], [undefined, "x"],
    ["Say", undefined], ["​فقط", "لا غير "]]) {
    for (const n of ["0", "1", "2500", "1250.75", "abc"]) for (const lang of LANGS) add({ input: n, unit: "SAR", prefix, suffix, lang });
  }
  // errors and odd input
  const bad = ["", " ", " \t", ",", "٬", ".", "..", "1.2.3", "1..2", "abc", "12a", "a12", "1e5", "1E5", "0x10", "-5", "−5",
    "+5", "++5", "+-5", "-0", "+", "-", "5-", "1 2", "1/2", "١٫٢٫٣", "1234567890123456", "0001234567890123456",
    "999999999999999", "1000000000000000", "1000000000000000.5", "0000000000000000001", "١٢٣٤٥٦٧٨٩٠١٢٣٤٥٦",
    "١۲", "😀", "۱۲۳٬۴۵۶٫۷۸", "12,34.5", "12.3,4", "１２", "½", "0.0000000000000001", "0." + "1".repeat(16), "0.000",
    "\ud83d", "1\udc00", "\u0085", "1\u00852", "﻿12", "1 2", "　", "١,٢٣٤.٥", "+.5", "-.5", "+٫٥", ".", "+.", "0.", "00.00",
    "٠", "۰۰", "١٢٣", "𝟏𝟐", "١‏٢", "1 000 000", "−−", "+−5", "1-", "1+", "NaN", "Infinity",
    "1_000", "1'000", "١٬٠٠٠٬٠٠٠٫٥٠", "999,999,999,999,999.99", "9".repeat(15) + ".5", "9".repeat(15) + ".995"];
  for (const s of bad) for (const unit of ["none", "SAR", "piece", "KWD", "nope", "BTC"]) add({ input: s, unit, lang: pick(LANGS) });
  // separators (SPEC §1.1): every shape in every language, and rounding to zero
  const sepInputs = ["12,50", "0,5", "1,250", "1.250", "1250,75", "1.250,75", "1,250.75", "1 250,75", "1 250.75", "1,250,000",
    "1.250.000", "1,2,3", "1,00,000", "0,250", "0.250", ",5", "5,", "1, 250", "1 ,250", "١٢٬٥٠", "١٬٢٥٠٫٧٥", "1٬250,000",
    "1,250 000", "1.5 000", "1,250.000,5", "12 50", "1234 567", "001,234", "1,,2", "1.,2", "1٫2,5", "1.2٫5", "1٫2٫3",
    "+ 12,50", "- 1,5", "12,50a", "1\u00a0250,75", "1\u202f250\u202f000", "1,000,000,000,000,000", "999,999,999,999,999.995",
    "0,004", "0.004", "0,0004", "0.0004", "0.000000004", "١٢٫٥٠", "١٢,٥٠", "۱٬۲۵۰", "1.25.000", "12 50,5", "1.250 000", "1,2500",
    "1.2500", "1\u3000250", "1_250,5", "1'250.5"];
  for (const s of sepInputs) {
    for (const lang of [undefined, "", ...LANGS, "de"]) for (const unit of ["SAR", "KWD", "none", "BTC"]) add({ input: s, unit, lang });
  }
  for (let i = 0; i < 6000; i++) {
    const ip = digits(1 + int(10));
    const t = pick([",", ".", " ", "٬", "\u00a0", ""]);
    let s = t ? group(ip, t) : ip;
    if (rand() < 0.15) s = s.replace(/(\d)(?=\d)/, (d) => d + pick([",", ".", " "])); // irregular
    if (rand() < 0.6) s += pick([".", ",", "٫"]) + digits(1 + int(4), false);
    add({ input: s, unit: pick(["SAR", "KWD", "EUR", "none", "TND", "piece"]), lang: pick([undefined, ...LANGS]), decimals: pick([undefined, "error"]) });
  }
  for (const unit of ["", " ", "XYZ", "ريالات", "sar ", " Sar", "S A R", "KM/H", "km/h", "Kg", "KG", "ＳＡＲ", "riyal​",
    "none", "None", "NONE", "بدون", "بدون  وحدة", "\u0085none", "﻿none", "᠎none", "sAr", "kWd", "ريال سعودي", "ريال سعودي",
    "ريال\u0085سعودي", "riyal ", "İ", "KİLOGRAM", undefined]) {
    for (const n of ["0", "1", "2", "12", "3.5"]) add({ input: n, unit, lang: pick(LANGS) });
  }
  return C;
}

function customCorpus() {
  const C = [];
  for (const unit of ["SAR", "KWD", "riyal", "BTC", "EUR", "lira", "none"]) {
    for (const input of ["0", "1", "2", "2.00", "2.50", "0.01", "1.01", "21", "1000000", "0.5", "12.05", "1.999", "abc", "2000000.5", "1.5"]) {
      for (const lang of LANGS) {
        C.push({ input, unit, lang, custom: true });
        C.push({ input, unit, lang, custom: true, decimals: "error" });
      }
    }
  }
  return C;
}

// ------------------------------------------------------------------ output

const SAFE = /[\p{L}\p{M}\p{N}\p{P}\p{S}]/u;
function enc(s) {
  if (s === undefined || s === null) return "~";
  let out = "";
  for (let i = 0; i < s.length; i++) {
    const ch = s[i];
    const c = s.charCodeAt(i);
    if (ch === "\\") out += "\\\\";
    else if (ch === "~") out += "\\u007E";
    else if (c > 0x20 && c < 0x7f) out += ch;
    else if (c === 0x20) out += " ";
    else if (c >= 0xa0 && (c < 0xd800 || c > 0xdfff) && SAFE.test(ch)) out += ch;
    else out += "\\u" + c.toString(16).toUpperCase().padStart(4, "0");
  }
  return out;
}

function run(c) {
  const engine = c.custom ? custom : { tafqit, tryTafqit };
  const opts = { unit: c.unit, hundredsForm: c.hf, prefix: c.prefix, suffix: c.suffix, decimals: c.decimals, language: c.lang };
  const r = engine.tryTafqit(c.input, opts);
  let message = "~";
  if (!r.ok) {
    // tryTafqit drops the detail; the throwing API keeps it in e.message.
    try {
      engine.tafqit(c.input, opts);
      throw new Error(`tafqit did not throw for ${JSON.stringify(c)}`);
    } catch (e) {
      if (e.code !== r.error.code) throw e;
      message = e.message;
    }
  }
  const warnings = r.ok && r.warnings ? r.warnings.map((w) => w.code).join(",") : undefined;
  const readings = !r.ok && r.error.readings ? r.error.readings.join("|") : undefined;
  return [
    "T", c.custom ? "1" : "0", enc(c.input), enc(c.unit), enc(c.hf), enc(c.prefix), enc(c.suffix), enc(c.decimals), enc(c.lang),
    r.ok ? "1" : "0", enc(r.ok ? r.result : r.error.code), r.ok ? "~" : enc(message), enc(r.cleaned), enc(warnings), enc(readings),
  ].join("\t");
}

/** normalizeNumber(input, lang); lang undefined = Tafqit.Clean(input). */
function clean(input, lang) {
  try {
    return ["C", enc(input), enc(lang), "1", enc(normalizeNumber(input, lang).cleaned)].join("\t");
  } catch (e) {
    return ["C", enc(input), enc(lang), "0", enc(e.code)].join("\t");
  }
}

const main = buildCorpus();
const extra = customCorpus();
const all = [...main, ...extra];
const lines = all.map(run);
const inputs = [...new Set(all.map((c) => c.input))];
const cleanLines = inputs.flatMap((input) => [undefined, "en", "fr"].map((lang) => clean(input, lang)));
const { sourceHash } = buildDotnet(JSON.parse(readFileSync(root + "data/units.json", "utf8")));

const header = [
  "# InstaWords .NET parity corpus. GENERATED by scripts/gen-dotnet-corpus.mjs from dist/index.js. Do not edit.",
  `# source-hash ${sourceHash}`,
  `# conversions ${lines.length}`,
  `# cleans ${cleanLines.length}`,
];
mkdirSync(dirname(outPath), { recursive: true });
writeFileSync(outPath, [...header, ...lines, ...cleanLines].join("\n") + "\n");
const ok = lines.filter((l) => l.split("\t")[9] === "1").length;
console.log(`${outPath}: ${lines.length} conversions (${ok} ok, ${lines.length - ok} errors, ${extra.length} on a custom catalog), ${cleanLines.length} cleans`);
