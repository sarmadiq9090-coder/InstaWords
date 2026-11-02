import { test, describe } from "node:test";
import assert from "node:assert/strict";
import { tafqit, tryTafqit, convert, createTafqit, normalizeNumber, findUnit, units, TafqitError } from "../src/index.ts";
import type { TafqitErrorCode, UnitCatalog } from "../src/index.ts";
import { parseCsv, toCsv, csvColumnValues } from "../src/csv.ts";

const r = (n: string, unit = "riyal", extra: object = {}) => tafqit(n, { unit, ...extra });
const throwsCode = (fn: () => unknown, code: TafqitErrorCode) =>
  assert.throws(fn, (e: unknown) => e instanceof TafqitError && e.code === code);

describe("number words, no unit", () => {
  const cases: [string, string][] = [
    ["0", "صفر"], ["1", "واحد"], ["2", "اثنان"], ["9", "تسعة"], ["14", "أربعة عشر"],
    ["18", "ثمانية عشر"], ["22", "اثنان وعشرون"], ["99", "تسعة وتسعون"], ["110", "مائة وعشرة"],
    ["200", "مائتان"], ["999", "تسعمائة وتسعة وتسعون"], ["2000", "ألفان"], ["2001", "ألفان وواحد"],
    ["10000", "عشرة آلاف"], ["12000", "اثنا عشر ألفاً"], ["21000", "واحد وعشرون ألفاً"],
    ["100000", "مائة ألف"], ["101000", "مائة وواحد ألف"], ["103000", "مائة وثلاثة آلاف"],
    ["111000", "مائة وأحد عشر ألفاً"], ["200000", "مائتا ألف"], ["999000", "تسعمائة وتسعة وتسعون ألفاً"],
    ["2000000", "مليونان"], ["3000000", "ثلاثة ملايين"], ["11000000", "أحد عشر مليوناً"],
    ["1000000000", "مليار"], ["2000000000", "ملياران"], ["5000000000", "خمسة مليارات"],
    ["1000000000000", "تريليون"], ["20000000000000", "عشرون تريليوناً"],
    ["1001001001001", "تريليون ومليار ومليون وألف وواحد"],
    ["999999999999999", "تسعمائة وتسعة وتسعون تريليوناً وتسعمائة وتسعة وتسعون ملياراً وتسعمائة وتسعة وتسعون مليوناً وتسعمائة وتسعة وتسعون ألفاً وتسعمائة وتسعة وتسعون"],
  ];
  for (const [n, want] of cases) test(n, () => assert.equal(tafqit(n), want));
});

describe("hundreds form", () => {
  test("all hundreds with مئة", () => {
    assert.deepEqual(
      ["100", "200", "300", "400", "500", "600", "700", "800", "900"].map((n) => tafqit(n, { hundredsForm: "مئة" })),
      ["مئة", "مئتان", "ثلاثمئة", "أربعمئة", "خمسمئة", "ستمئة", "سبعمئة", "ثمانمئة", "تسعمئة"],
    );
  });
  test("construct with مئة", () => {
    assert.equal(r("200", "riyal", { hundredsForm: "مئة" }), "مئتا ريال");
    assert.equal(tafqit("200000", { hundredsForm: "مئة" }), "مئتا ألف");
  });
  test("invalid form", () => throwsCode(() => tafqit("1", { hundredsForm: "x" as never }), "INVALID_OPTION"));
});

describe("counted noun (masculine)", () => {
  const cases: [string, string][] = [
    ["4", "أربعة ريالات"], ["22", "اثنان وعشرون ريالاً"], ["99", "تسعة وتسعون ريالاً"],
    ["102", "مائة واثنان ريال"], ["103", "مائة وثلاثة ريالات"], ["110", "مائة وعشرة ريالات"],
    ["111", "مائة وأحد عشر ريالاً"], ["250", "مائتان وخمسون ريالاً"], ["300", "ثلاثمائة ريال"],
    ["1001", "ألف وواحد ريال"], ["2005", "ألفان وخمسة ريالات"], ["10000", "عشرة آلاف ريال"],
    ["12000", "اثنا عشر ألف ريال"], ["100000", "مائة ألف ريال"], ["2000000", "مليونا ريال"],
    ["3000000", "ثلاثة ملايين ريال"], ["11000000", "أحد عشر مليون ريال"], ["1000200", "مليون ومائتا ريال"],
    ["1011000", "مليون وأحد عشر ألف ريال"], ["11000001", "أحد عشر مليوناً وواحد ريال"],
    ["2000000000", "مليارا ريال"], ["2000000000000", "تريليونا ريال"],
  ];
  for (const [n, want] of cases) test(n, () => assert.equal(r(n), want));
});

describe("counted noun (feminine)", () => {
  const cases: [string, string][] = [
    ["0", "صفر ليرة"], ["4", "أربع ليرات"], ["10", "عشر ليرات"], ["12", "اثنتا عشرة ليرة"],
    ["18", "ثماني عشرة ليرة"], ["19", "تسع عشرة ليرة"], ["22", "اثنتان وعشرون ليرة"],
    ["28", "ثماني وعشرون ليرة"], ["31", "إحدى وثلاثون ليرة"], ["100", "مائة ليرة"],
    ["101", "مائة وواحدة ليرة"], ["1003", "ألف وثلاث ليرات"], ["3000", "ثلاثة آلاف ليرة"],
    ["13000", "ثلاثة عشر ألف ليرة"], ["13013", "ثلاثة عشر ألفاً وثلاث عشرة ليرة"],
  ];
  for (const [n, want] of cases) test(n, () => assert.equal(r(n, "lira"), want));
});

describe("fractions", () => {
  test("SAR 0.01", () => assert.equal(r("0.01", "SAR"), "هللة واحدة"));
  test("SAR 0.02", () => assert.equal(r("0.02", "SAR"), "هللتان"));
  test("SAR 1.10", () => assert.equal(r("1.10", "SAR"), "ريال سعودي واحد وعشر هللات"));
  test("SAR 0.00", () => assert.equal(r("0.00", "SAR"), "صفر ريال سعودي"));
  test("KWD 0.001", () => assert.equal(r("0.001", "KWD"), "فلس واحد"));
  test("KWD 0.002", () => assert.equal(r("0.002", "KWD"), "فلسان"));
  test("KWD 1.2", () => assert.equal(r("1.2", "KWD"), "دينار كويتي واحد ومائتا فلس"));
  test("BHD 3.011", () => assert.equal(r("3.011", "BHD"), "ثلاثة دنانير بحرينية وأحد عشر فلساً"));
  test("JOD 2.5", () => assert.equal(r("2.5", "JOD"), "ديناران أردنيان وخمسمائة فلس"));
  test("OMR 1.001", () => assert.equal(r("1.001", "OMR"), "ريال عماني واحد وبيسة واحدة"));
  test("too many decimals", () => throwsCode(() => r("10.125", "SAR"), "TOO_MANY_DECIMALS"));
  test("trailing zeros beyond precision are fine", () => assert.equal(r("10.12000", "SAR"), "عشرة ريالات سعودية واثنتا عشرة هللة"));
  test("fraction on unit without subunit", () => throwsCode(() => r("2.5", "piece"), "FRACTION_NOT_SUPPORTED"));
  test("zero fraction on unit without subunit", () => assert.equal(r("2.0", "piece"), "قطعتان"));
  test("no unit decimals", () => {
    assert.equal(tafqit("12.05"), "اثنا عشر فاصلة صفر خمسة");
    assert.equal(tafqit("0.5"), "صفر فاصلة خمسة");
    assert.equal(tafqit("3.125"), "ثلاثة فاصلة مائة وخمسة وعشرون");
  });
});

describe("normalization", () => {
  const cases: [string, string][] = [
    [" 1 500 ", "1500"], ["١٬٢٣٤٫٥٠", "1234.5"], ["۱۲۳", "123"], ["007", "7"], ["0007.0100", "7.01"],
    [".5", "0.5"], ["5.", "5"], ["+12", "12"], ["1,000,000", "1000000"], [" 1 000", "1000"],
  ];
  for (const [n, want] of cases) test(JSON.stringify(n), () => assert.equal(normalizeNumber(n).cleaned, want));
  test("numbers", () => {
    assert.equal(normalizeNumber(1500).cleaned, "1500");
    assert.equal(normalizeNumber(12.5).cleaned, "12.5");
    assert.equal(normalizeNumber(123456789012345n).cleaned, "123456789012345");
  });
  const errors: [string, TafqitErrorCode][] = [
    ["", "EMPTY"], ["   ", "EMPTY"], ["abc", "INVALID_FORMAT"], ["1.2.3", "INVALID_FORMAT"], [".", "INVALID_FORMAT"],
    ["1e5", "INVALID_FORMAT"], ["-5", "NEGATIVE"], ["−5", "NEGATIVE"], ["1000000000000000", "TOO_LARGE"],
  ];
  for (const [n, code] of errors) test(`error ${JSON.stringify(n)}`, () => throwsCode(() => normalizeNumber(n), code));
  test("leading zeros do not count toward the limit", () => assert.equal(normalizeNumber("0000999999999999999").integer.length, 15));
});

describe("API", () => {
  test("prefix/suffix trimmed and optional", () => {
    assert.equal(r("5", "riyal", { prefix: " فقط ", suffix: "" }), "فقط خمسة ريالات");
    assert.equal(r("5", "riyal", { suffix: "لا غير" }), "خمسة ريالات لا غير");
  });
  test("unit lookup by alias, label, code, case-insensitive id", () => {
    assert.equal(tafqit("3", { unit: "ريال سعودي" }), "ثلاثة ريالات سعودية");
    assert.equal(tafqit("3", { unit: "sar" }), "ثلاثة ريالات سعودية");
    assert.equal(tafqit("3", { unit: "  ريال   سعودي " }), "ثلاثة ريالات سعودية");
    assert.equal(findUnit("دينار كويتي")?.id, "KWD");
  });
  test("unknown unit", () => throwsCode(() => tafqit("1", { unit: "xyz" }), "UNKNOWN_UNIT"));
  test("tryTafqit", () => {
    assert.deepEqual(tryTafqit("١٢", { unit: "riyal" }), { ok: true, result: "اثنا عشر ريالاً", cleaned: "12" });
    const bad = tryTafqit("10.125", { unit: "SAR" });
    assert.equal(bad.ok, false);
    if (!bad.ok) { assert.equal(bad.error.code, "TOO_MANY_DECIMALS"); assert.equal(bad.cleaned, "10.125"); }
    const bad2 = tryTafqit("x");
    assert.ok(!bad2.ok && bad2.cleaned === undefined);
  });
  test("convert()", () => {
    assert.deepEqual(convert({ the_number: "200", unit: "riyal", hundreds_form: "مئة" }), { result: "مئتا ريال" });
    assert.deepEqual(convert({ the_number: 7 }), { result: "سبعة" });
  });
  test("units list contains none and is non-empty", () => {
    assert.ok(units.length > 10);
    assert.equal(units.find((u) => u.id === "none")?.forms, null);
  });
});

describe("custom catalogs", () => {
  const base: UnitCatalog = {
    schemaVersion: 1,
    units: [
      { id: "none", category: "none", label: "بدون وحدة", forms: null },
      { id: "box", category: "count", label: "صندوق", forms: { singular: "صندوق", dual: "صندوقان", plural: "صناديق", accusative: "صندوقاً", gender: "m" } },
    ],
  };
  test("extends without code changes", () => {
    const t = createTafqit(base);
    assert.equal(t.tafqit("15", { unit: "box" }), "خمسة عشر صندوقاً");
    assert.equal(t.tafqit("7", { unit: "صندوق" }), "سبعة صناديق");
  });
  const bad = (mut: (c: UnitCatalog) => void) => {
    const c = structuredClone(base); mut(c); return () => createTafqit(c);
  };
  test("duplicate id", () => throwsCode(bad((c) => c.units.push(structuredClone(c.units[1]!))), "INVALID_CATALOG"));
  test("duplicate alias across units", () => throwsCode(bad((c) => { c.units[1]!.aliases = ["بدون وحدة"]; }), "INVALID_CATALOG"));
  test("bad gender", () => throwsCode(bad((c) => { (c.units[1]!.forms as { gender: string }).gender = "x"; }), "INVALID_CATALOG"));
  test("empty form", () => throwsCode(bad((c) => { c.units[1]!.forms!.plural = ""; }), "INVALID_CATALOG"));
  test("bad ratio", () => throwsCode(bad((c) => {
    c.units[1]!.subunit = { singular: "a", dual: "b", plural: "c", accusative: "d", gender: "m", ratio: 60 };
  }), "INVALID_CATALOG"));
  test("bad schemaVersion", () => throwsCode(bad((c) => { (c as { schemaVersion: number }).schemaVersion = 2; }), "INVALID_CATALOG"));
});

describe("output hygiene over a range", () => {
  test("no double spaces, no dangling و, starts/ends cleanly (0..20000 and samples)", () => {
    const samples: string[] = [];
    for (let i = 0; i <= 20000; i++) samples.push(String(i));
    for (let i = 1; i < 2000; i++) samples.push(String(BigInt(i) * 7919n * 1000003n));
    for (const unit of ["none", "riyal", "lira", "SAR"]) {
      for (const n of samples) {
        const s = tafqit(n, { unit });
        assert.ok(!/\s{2}|^\s|\s$|و\s|\sو$/.test(s), `${unit} ${n}: «${s}»`);
      }
    }
  });
});

describe("csv", () => {
  test("parse quoted, CRLF, BOM", () => {
    assert.deepEqual(parseCsv('﻿a,"b,1","c""d"\r\n1,2,3\n'), [["a", "b,1", 'c"d'], ["1", "2", "3"]]);
  });
  test("column values with header and unquoted thousands", () => {
    assert.deepEqual(csvColumnValues("المبلغ\n1,500\n\"2,000.50\"\n\n١٢٣\n"), ["1,500", "2,000.50", "١٢٣"]);
    assert.deepEqual(csvColumnValues("10;\n20;\n"), ["10", "20"]);
  });
  test("toCsv escapes and adds BOM", () => {
    assert.equal(toCsv([["a", "b,c"], ['x"y', "z"]]), '﻿a,"b,c"\r\n"x""y",z\r\n');
  });
});
