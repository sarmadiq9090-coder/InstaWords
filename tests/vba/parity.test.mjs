// Parity: the generated VBA module (run through tests/vba/harness) must produce
// exactly the same text as the TypeScript core (dist/index.js).
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { ERROR_MESSAGES, normalizeNumber, tryTafqit, units } from "../../dist/index.js";
import { loadTafqitVba } from "./harness/load.mjs";
import { EMPTY, VDecimal, VErrorValue, VObject, VTyped } from "./harness/vba.mjs";

const vm = loadTafqitVba();
const golden = JSON.parse(readFileSync(new URL("../golden/cases.json", import.meta.url), "utf8"));

const HF = ["مائة", "مئة"];

/** VBA call: TAFQIT(input, unit, hf, prefix, suffix, decimals). */
const vba = (c) => vm.call("TAFQIT", c.input, c.unit ?? "none", c.hf ?? "", c.prefix ?? "", c.suffix ?? "", c.decimals ?? "");

/** The same request through the core; errors become ERROR_MESSAGES[code] (no detail). */
function core(c) {
  const hundredsForm = c.hf === "1" ? HF[0] : c.hf === "2" ? HF[1] : c.hf === "" || c.hf === undefined ? undefined : c.hf;
  // VBA accepts the Decimals option trimmed and in any case; "" means the default.
  const dec = c.decimals === undefined ? "" : c.decimals.trim().toLowerCase();
  const decimals = dec === "" ? undefined : dec === "round" || dec === "error" ? dec : c.decimals;
  const r = tryTafqit(c.input, { unit: c.unit, hundredsForm, prefix: c.prefix, suffix: c.suffix, decimals });
  return r.ok ? r.result : ERROR_MESSAGES[r.error.code];
}

function coreClean(input) {
  try {
    return normalizeNumber(input).cleaned;
  } catch (e) {
    return ERROR_MESSAGES[e.code];
  }
}

// ------------------------------------------------------------------ corpus

function rng(seed) {
  return () => {
    seed = (seed + 0x6d2b79f5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
const rand = rng(20261102);
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

function buildCorpus() {
  const C = [];
  const add = (c) => C.push(c);

  // every integer 0..2000 with: no unit, masculine, feminine, unit with subunit, both hundreds forms
  for (let n = 0; n <= 2000; n++) {
    for (const unit of ["none", "riyal", "lira", "SAR"]) add({ input: String(n), unit });
    if (n % 3 === 0) add({ input: String(n), unit: pick(unitIds), hf: HF[1] });
  }
  // random 1..15 digit integers (and some with zero groups)
  for (let i = 0; i < 3000; i++) {
    let s = digits(1 + int(15));
    if (i % 4 === 0) s = s.replace(/\d{3}(?=(\d{3})*$)/g, (g) => (rand() < 0.5 ? "000" : g)).replace(/^0+/, "") || "0";
    add({ input: s, unit: pick(unitIds), hf: pick(["", ...HF]) });
  }
  // fractions
  for (let i = 0; i < 3000; i++) {
    const ip = rand() < 0.2 ? "0" : digits(1 + int(i % 10 === 0 ? 15 : 7));
    const fp = digits(1 + int(i % 7 === 0 ? 16 : 9), false);
    const unit = i % 5 === 0 ? "none" : i % 11 === 0 ? pick(withoutSub) : pick(withSub);
    add({ input: `${rand() < 0.1 ? "" : ip}.${fp}`, unit, hf: pick(["", ...HF]), decimals: pick(["", "", "round", "error"]) });
  }
  // rounding carries and limits
  for (const unit of ["SAR", "KWD", "BTC", "meter", "none"]) {
    for (const s of ["0.995", "0.9999", "9.995", "99.9951", "999.9995", "1.005", "1.0049", "0.0049", "0.005",
      "999999999999999.99", "999999999999999.995", "999999999999999.9999", "99999999999999.999", "0.00000000049",
      "0.000000005", "123.4500001", "5.", ".5", "0.999"]) {
      for (const decimals of ["", "error"]) add({ input: s, unit, decimals });
    }
  }
  // input formats: Indic/Persian digits, separators, spaces, signs, zeros
  const indic = (s) => s.replace(/\d/g, (d) => String.fromCharCode(0x660 + Number(d)));
  const persian = (s) => s.replace(/\d/g, (d) => String.fromCharCode(0x6f0 + Number(d)));
  const group = (s, sep) => s.replace(/\B(?=(\d{3})+(?!\d))/g, sep);
  const spaces = [" ", "\u00a0", "\u2009", "\u202f", "\t", "\r", "\n", "\u3000", "\u2002", "\ufeff"];
  for (let i = 0; i < 2500; i++) {
    const ip = digits(1 + int(12));
    const fp = rand() < 0.5 ? "" : digits(1 + int(3), false);
    let s = rand() < 0.5 ? group(ip, pick([",", "٬", " ", "\u00a0", "\u202f", "\u2009"])) : ip;
    if (fp) s += pick([".", "٫", ".", "٫"]) + fp + (rand() < 0.3 ? "00" : "");
    if (rand() < 0.3) s = "00" + s;
    const f = int(4);
    if (f === 1) s = indic(s);
    if (f === 2) s = persian(s);
    if (f === 3) s = s.split("").map((ch) => (rand() < 0.5 ? indic(ch) : persian(ch))).join("");
    if (rand() < 0.3) s = pick(spaces) + s + pick(spaces);
    if (rand() < 0.1) s = pick(["+", "-", "\u2212", "++", "+-"]) + s;
    add({ input: s, unit: pick(["none", "SAR", "KWD", "riyal", "lira", "piece"]) });
  }
  // every unit: numbers through all count forms, N=1 (incl. the "one" form), lookups by every name
  const nums = ["0", "1", "2", "3", "8", "10", "11", "19", "20", "21", "28", "99", "100", "101", "102", "103", "111",
    "200", "201", "1000", "1001", "2000", "2002", "3000", "11000", "100000", "101000", "102000", "200000", "1000000",
    "2000000", "11000000", "200000000", "1000000000", "2000000000000", "999999999999999",
    "0.1", "0.01", "0.02", "0.5", "1.01", "1.02", "2.03", "12.345", "2.5", "0.001", "1.000001", "3.11"];
  for (const u of units) {
    for (const n of nums) for (const hf of ["", HF[1]]) add({ input: n, unit: u.id, hf });
    const names = [u.id, u.label, u.code, ...(u.aliases ?? [])].filter(Boolean);
    for (const name of names) {
      add({ input: "1", unit: name });
      add({ input: "15", unit: `  ${name.toUpperCase()}\u00a0` });
      add({ input: "2", unit: name.replace(/ /g, " \t "), hf: "2" });
      add({ input: "1", unit: name.toLowerCase() });
    }
    if (u.subunit) for (const sub of ["0.1", "0.01", "1.1", "0." + "0".repeat(String(u.subunit.ratio).length - 2) + "1"]) add({ input: sub, unit: u.id });
  }
  // hundreds forms and options
  for (const n of ["100", "200", "300", "900", "200000", "125", "1250.75"]) {
    for (const hf of ["", "1", "2", ...HF, "x", " مئة", "مائه", "3", "مائة "]) add({ input: n, unit: "SAR", hf });
    for (const decimals of ["", "round", "error", "ROUND", " Error ", "x", "rounded"]) add({ input: n + ".125", unit: "SAR", decimals });
  }
  // prefix / suffix
  for (const [prefix, suffix] of [["فقط", "لا غير"], ["  فقط ", " لا غير  "], ["\u3000فقط\u00a0", "\tلا غير\n"], ["", "لا غير"],
    ["فقط", ""], ["   ", "\u00a0"], ["Only", "only."], ["\ufeffفقط", "لا  غير"]]) {
    for (const n of ["0", "1", "2500", "1250.75", "abc"]) add({ input: n, unit: "SAR", prefix, suffix });
  }
  // errors and odd input
  const bad = ["", " ", "\u00a0\t", ",", "٬", ".", "..", "1.2.3", "1..2", "abc", "12a", "a12", "1e5", "1E5", "0x10", "-5", "\u22125",
    "+5", "++5", "+-5", "-0", "+", "-", "5-", "1 2", "1/2", "١٫٢٫٣", "1234567890123456", "0001234567890123456",
    "999999999999999", "1000000000000000", "1000000000000000.5", "0000000000000000001", "١٢٣٤٥٦٧٨٩٠١٢٣٤٥٦",
    "١۲", "\ud83d\ude00", "۱۲۳٬۴۵۶٫۷۸", "12,34.5", "12.3,4", "\uff11\uff12", "½", "0.0000000000000001", "0." + "1".repeat(16), "0.000"];
  for (const s of bad) for (const unit of ["none", "SAR", "piece", "KWD", "nope"]) add({ input: s, unit });
  for (const unit of ["", " ", "XYZ", "ريالات", "sar ", " Sar", "S A R", "KM/H", "\u212ag", "\u212aG", "\uff33\uff21\uff32", "riyal\u200b", "none", "None", "بدون"]) {
    for (const n of ["0", "1", "2", "12", "3.5"]) add({ input: n, unit });
  }
  return C;
}

const corpus = buildCorpus();

// ------------------------------------------------------------------ tests

test("golden cases: VBA output equals expected", () => {
  const d = golden.defaults ?? {};
  for (const c of golden.cases) {
    const o = { ...d, ...c.options };
    const got = vba({ input: c.input, unit: c.unit, hf: o.hundredsForm, prefix: o.prefix, suffix: o.suffix, decimals: o.decimals });
    assert.equal(got, c.expected, `${c.id}: ${c.input} [${c.unit}]`);
    assert.equal(vm.call("TAFQIT_CLEAN", c.input), c.cleaned, `${c.id} cleaned`);
  }
});

test("TafqitSelfTest (generated VBA test module) passes every golden case", () => {
  vm.output.length = 0;
  vm.messages.length = 0;
  vm.call("TafqitSelfTest");
  const n = golden.cases.length;
  assert.equal(vm.messages.length, 1);
  assert.equal(vm.messages[0].prompt, `InstaWords self-test: ${n} of ${n} golden cases passed.`);
  assert.deepEqual(vm.output, [vm.messages[0].prompt]);
});

test(`parity corpus: ${corpus.length} cases, VBA TAFQIT == core`, () => {
  assert.ok(corpus.length > 20000, `corpus too small: ${corpus.length}`);
  const failures = [];
  const seen = new Map();
  for (const c of corpus) {
    const want = core(c);
    seen.set(want, (seen.get(want) ?? 0) + 1);
    let got;
    try {
      got = vba(c);
    } catch (e) {
      got = `<${e.message}>`;
    }
    if (got !== want) failures.push({ ...c, want, got });
  }
  assert.deepEqual(failures.slice(0, 10), [], `${failures.length} mismatches`);
  // The corpus must exercise successes and every error the VBA can return.
  for (const [code, msg] of Object.entries(ERROR_MESSAGES)) {
    if (code !== "INVALID_CATALOG") assert.ok((seen.get(msg) ?? 0) >= 5, `corpus has too few ${code} cases`);
  }
  const errors = new Set(Object.values(ERROR_MESSAGES));
  const okCount = [...seen].filter(([t]) => !errors.has(t)).reduce((n, [, k]) => n + k, 0);
  assert.ok(okCount > 20000, `only ${okCount} successful cases`);
});

test("parity corpus: VBA TAFQIT_CLEAN == normalizeNumber().cleaned", () => {
  const failures = [];
  for (const input of new Set(corpus.map((c) => c.input))) {
    const want = coreClean(input);
    const got = vm.call("TAFQIT_CLEAN", input);
    if (got !== want) failures.push({ input, want, got });
  }
  assert.deepEqual(failures.slice(0, 10), [], `${failures.length} mismatches`);
});

test("every unit at N=1 (including units with a 'one' form)", () => {
  for (const u of units) {
    assert.equal(vba({ input: "1", unit: u.id }), core({ input: "1", unit: u.id }), u.id);
    if (u.forms?.one) assert.equal(vba({ input: "1", unit: u.id }), u.forms.one);
  }
});

// Numeric cells: VBA-only behaviour (the core receives these as text).
class FakeRange extends VObject {
  constructor(value) { super(); this.value = value; }
  Cells() { return this; }
  get Value2() { return this.value; }
}

test("numeric and cell arguments", () => {
  const same = (arg, text, unit = "SAR") => assert.equal(vm.call("TAFQIT", arg, unit), core({ input: text, unit }), `${String(arg)} as ${text}`);
  same(1250.75, "1250.75");
  same(0.1 + 0.2, "0.3"); // CDec keeps 15 significant digits, as Excel displays
  same(2500, "2500");
  same(1e-7, "0.0000001", "none");
  same(123456789012345, "123456789012345");
  same(new VTyped(3, 42), "42");
  same(new VDecimal("10.125"), "10.125");
  same(new FakeRange(15), "15");
  same(new FakeRange("١٢٣٫٤٥"), "123.45");
  same(-5, "-5");
  same(1e20, "100000000000000000000");
  same(1e300, "100000000000000000000");
  same(EMPTY, "");
  same(new FakeRange(EMPTY), "");
  assert.equal(vm.call("TAFQIT", true, "SAR"), ERROR_MESSAGES.INVALID_FORMAT);
  const na = new VErrorValue(2042);
  assert.equal(vm.call("TAFQIT", na, "SAR"), na);
  assert.equal(vm.call("TAFQIT", "5", new FakeRange(na)), na);
  assert.equal(vm.call("TAFQIT", 5, new FakeRange("SAR"), new FakeRange(2)), core({ input: "5", unit: "SAR", hf: "2" }));
  assert.equal(vm.call("TAFQIT", 5), core({ input: "5", unit: "none" }));
  assert.equal(vm.call("TAFQIT_CLEAN", 1250.5), "1250.5");
  assert.equal(vm.call("TAFQIT_CLEAN", new FakeRange("٠٠١٬٢٣٤٫٥٠")), "1234.5");
});
