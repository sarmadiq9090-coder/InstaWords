import { test, describe } from "node:test";
import assert from "node:assert/strict";
import {
  tafqit, tryTafqit, convert, createTafqit, normalizeNumber, findUnit, units, TafqitError, ERROR_MESSAGES, LANGUAGES,
  WARNING_MESSAGES,
} from "../src/index.ts";
import type { Language, TafqitErrorCode, UnitCatalog } from "../src/index.ts";
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
    assert.equal(r("200", "riyal", { hundredsForm: "مئة" }), "مئتان ريال");
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
    ["12000", "اثنا عشر ألف ريال"], ["100000", "مائة ألف ريال"], ["2000000", "مليونان ريال"],
    ["3000000", "ثلاثة ملايين ريال"], ["11000000", "أحد عشر مليون ريال"], ["1000200", "مليون ومائتان ريال"],
    ["1011000", "مليون وأحد عشر ألف ريال"], ["11000001", "أحد عشر مليوناً وواحد ريال"],
    ["2000000000", "ملياران ريال"], ["2000000000000", "تريليونان ريال"], ["200200", "مائتا ألف ومائتان ريال"],
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
  test("KWD 1.2", () => assert.equal(r("1.2", "KWD"), "دينار كويتي واحد ومائتان فلس"));
  test("BHD 3.011", () => assert.equal(r("3.011", "BHD"), "ثلاثة دنانير بحرينية وأحد عشر فلساً"));
  test("JOD 2.5", () => assert.equal(r("2.5", "JOD"), "ديناران أردنيان وخمسمائة فلس"));
  test("OMR 1.001", () => assert.equal(r("1.001", "OMR"), "ريال عماني واحد وبيسة واحدة"));
  test("too many decimals: error policy", () => throwsCode(() => r("10.125", "SAR", { decimals: "error" }), "TOO_MANY_DECIMALS"));
  test("rounding half-up (default)", () => {
    assert.equal(r("10.124", "SAR"), "عشرة ريالات سعودية واثنتا عشرة هللة");
    assert.equal(r("10.125", "SAR"), "عشرة ريالات سعودية وثلاث عشرة هللة");
    assert.equal(r("1.995", "SAR"), "ريالان سعوديان");
    assert.equal(r("0.004", "SAR"), "صفر ريال سعودي");
    assert.equal(r("0.005", "SAR"), "هللة واحدة");
    assert.equal(r("9.9999", "KWD"), "عشرة دنانير كويتية");
    assert.equal(tafqit(0.1 + 0.2, { unit: "SAR" }), "ثلاثون هللة");
    throwsCode(() => r("999999999999999.995", "SAR"), "TOO_LARGE");
    throwsCode(() => r("1", "SAR", { decimals: "x" }), "INVALID_OPTION");
  });
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
    ["", "EMPTY"], ["   ", "EMPTY"], ["abc", "INVALID_FORMAT"], ["1.2.3", "AMBIGUOUS_SEPARATOR"], [".", "INVALID_FORMAT"],
    ["1e5", "INVALID_FORMAT"], ["-5", "NEGATIVE"], ["−5", "NEGATIVE"], ["1000000000000000", "TOO_LARGE"],
  ];
  for (const [n, code] of errors) test(`error ${JSON.stringify(n)}`, () => throwsCode(() => normalizeNumber(n), code));
  test("leading zeros do not count toward the limit", () => assert.equal(normalizeNumber("0000999999999999999").integer.length, 15));
});

describe("separators (SPEC §1.1): never guess silently", () => {
  const clean = (n: string, language?: Language) => normalizeNumber(n, language).cleaned;
  const ambiguous = (n: string, language: Language | undefined, readings: string[]) => {
    assert.throws(() => normalizeNumber(n, language), (e: unknown) => {
      assert.ok(e instanceof TafqitError, String(e));
      assert.equal(e.code, "AMBIGUOUS_SEPARATOR", `${n} (${language})`);
      assert.deepEqual(e.readings, readings, `${n} (${language})`);
      const q = readings.length ? ` (هل تقصد ${readings.join(" أم ")}؟)` : "";
      assert.equal(e.message, ERROR_MESSAGES.AMBIGUOUS_SEPARATOR + q);
      return true;
    });
  };

  describe("unambiguous input works in every language", () => {
    const ok: [string, string][] = [
      ["1250.75", "1250.75"], ["1,250.75", "1250.75"], ["1,250,000", "1250000"], ["1,250,000.5", "1250000.5"],
      ["١٢٥٠٫٧٥", "1250.75"], ["١٬٢٥٠٫٧٥", "1250.75"], ["١٬٢٥٠٬٠٠٠", "1250000"], ["۱٬۲۵۰٫۷۵", "1250.75"],
      ["1 250 000", "1250000"], ["1\u00a0250\u00a0000.5", "1250000.5"], ["1\u202f250.75", "1250.75"], ["1\u2009250", "1250"],
      ["12345", "12345"], ["0.5", "0.5"], [".5", "0.5"], ["5.", "5"], ["0.004", "0.004"], ["999,999,999,999,999", "999999999999999"],
      ["1 ,250.5", "1250.5"], ["+ 1 500", "1500"], ["12,500.5", "12500.5"], ["\t1,234.50\n", "1234.5"],
    ];
    for (const lang of [undefined, "ar", "en", "fr"] as const) {
      for (const [input, want] of ok) {
        test(`${JSON.stringify(input)} (${lang ?? "default"}) = ${want}`, () => assert.equal(clean(input, lang), want));
      }
    }
  });

  describe("Arabic and English: «,» is a thousands separator in groups of three, «.» the decimal point", () => {
    for (const lang of [undefined, "ar", "en"] as const) {
      test(`B1/B2 examples (${lang ?? "default"})`, () => {
        ambiguous("12,50", lang, ["12.50", "1250"]);
        ambiguous("0,5", lang, ["0.5", "5"]);
        ambiguous("1250,75", lang, ["1250.75", "125075"]);
        ambiguous("1.250,75", lang, ["1250.75", "1.25075"]);
        ambiguous("1 250,75", lang, ["1250.75", "125075"]);
        ambiguous("1,2,3", lang, ["123"]);
        ambiguous("1,00,000", lang, ["100000"]);
        ambiguous("1.250.000", lang, ["1250000"]);
        ambiguous("0,250", lang, ["0.250", "250"]);
        ambiguous("1,2500", lang, ["1.2500", "12500"]);
      });
    }
    test("mixed or irregular grouping", () => {
      ambiguous("1,250 000", "ar", ["1250000", "1.250000"]);
      ambiguous("1 250,000", "en", ["1250.000", "1250000"]);
      ambiguous("١٢٬٥٠", "ar", ["1250"]);
      ambiguous("1٬250,000", "ar", ["1250.000", "1250000"]);
      ambiguous("12 50", "ar", ["1250"]);
      ambiguous("1 2", "ar", ["12"]);
      ambiguous("1234 567", "ar", ["1234567"]);
      ambiguous("5,", "ar", ["5"]);
      ambiguous(",5", "ar", ["0.5", "5"]);
      ambiguous("1.5 000", "ar", ["1.5000", "15000"]);
      ambiguous("1.250,", "en", ["1250", "1.250"]);
    });
    test("still fine: one comma before exactly three digits is thousands", () => {
      assert.equal(clean("1,250"), "1250");
      assert.equal(clean("1, 250", "ar"), "1250");
      assert.equal(clean("001,234", "en"), "1234");
      assert.equal(clean("15,000", "en"), "15000");
      assert.equal(clean("1.250", "ar"), "1.25");
    });
  });

  describe("French: «,» is the decimal point (the French convention)", () => {
    const fr: [string, string][] = [
      ["12,50", "12.5"], ["0,5", "0.5"], ["1250,75", "1250.75"], ["1 250,75", "1250.75"], ["1.250,75", "1250.75"],
      ["1\u00a0250\u00a0000,50", "1250000.5"], ["1\u202f250,75", "1250.75"], ["1.250.000", "1250000"], ["1,250,000", "1250000"],
      ["1,250.75", "1250.75"], ["1,250", "1.25"], ["1.250", "1.25"], ["1,125", "1.125"], ["0,250", "0.25"], [",5", "0.5"],
      ["5,", "5"], ["١٢٫٥", "12.5"], ["١٬٢٥٠٫٧٥", "1250.75"], ["1 250", "1250"], ["2 500", "2500"], ["12,345678", "12.345678"],
      ["1.250.000,5", "1250000.5"], ["1,250,000.5", "1250000.5"],
    ];
    for (const [input, want] of fr) {
      test(`${JSON.stringify(input)} = ${want}`, () => assert.equal(clean(input, "fr"), want));
    }
    test("ambiguous in French too", () => {
      ambiguous("1,250.000,5", "fr", ["1250.0005"]);
      ambiguous("1,2,3", "fr", ["123"]);
      ambiguous("1.25.000", "fr", ["125000"]);
      ambiguous("12 50,5", "fr", ["1250.5", "12505"]);
      ambiguous("1,5 000", "fr", ["15000", "1.5000"]);
      ambiguous("1.250 000", "fr", ["1.250000", "1250000"]);
    });
  });

  test("the words follow the convention of the output language", () => {
    assert.equal(tafqit("12,50", { unit: "EUR", language: "fr" }), "douze euros et cinquante centimes");
    assert.equal(tafqit("0,5", { unit: "TND", language: "fr" }), "cinq cents millimes");
    assert.equal(tafqit("1.250,75", { unit: "SAR", language: "fr" }), "mille deux cent cinquante riyals saoudiens et soixante-quinze halalas");
    throwsCode(() => tafqit("12,50", { unit: "EUR" }), "AMBIGUOUS_SEPARATOR");
    throwsCode(() => tafqit("0,5", { unit: "TND", language: "ar" }), "AMBIGUOUS_SEPARATOR");
    throwsCode(() => tafqit("1.250,75", { unit: "SAR", language: "en" }), "AMBIGUOUS_SEPARATOR");
  });

  test("numbers and bigints are never ambiguous", () => {
    assert.equal(normalizeNumber(1.25, "fr").cleaned, "1.25");
    assert.equal(normalizeNumber(1.125, "fr").cleaned, "1.125");
    assert.equal(normalizeNumber(1250, "fr").cleaned, "1250");
    assert.equal(normalizeNumber(1250n, "fr").cleaned, "1250");
    assert.equal(tafqit(1.125, { unit: "KWD", language: "fr" }), "un dinar koweïtien et cent vingt-cinq fils");
  });

  test("order of errors: EMPTY, NEGATIVE and INVALID_FORMAT come first, TOO_LARGE after", () => {
    throwsCode(() => normalizeNumber("  "), "EMPTY");
    throwsCode(() => normalizeNumber("-12,50"), "NEGATIVE");
    throwsCode(() => normalizeNumber("12,50a"), "INVALID_FORMAT");
    throwsCode(() => normalizeNumber("1,,250"), "INVALID_FORMAT");
    throwsCode(() => normalizeNumber("1.,250"), "INVALID_FORMAT");
    throwsCode(() => normalizeNumber("1٫250٫5"), "INVALID_FORMAT");
    throwsCode(() => normalizeNumber("1.250٫5"), "INVALID_FORMAT");
    throwsCode(() => normalizeNumber("1٫2,5", "fr"), "INVALID_FORMAT");
    throwsCode(() => normalizeNumber(","), "INVALID_FORMAT");
    throwsCode(() => normalizeNumber(", ."), "INVALID_FORMAT");
    throwsCode(() => normalizeNumber("1,000,000,000,000,000"), "TOO_LARGE");
    throwsCode(() => normalizeNumber("1 000 000 000 000 000,5", "fr"), "TOO_LARGE");
    throwsCode(() => normalizeNumber("1,000,000,000,000,00"), "AMBIGUOUS_SEPARATOR");
    throwsCode(() => normalizeNumber("5", "de" as Language), "INVALID_OPTION");
    assert.equal(normalizeNumber("5", "" as Language).cleaned, "5");
  });

  test("tryTafqit returns the readings; the message asks which one was meant", () => {
    const x = tryTafqit("12,50", { unit: "EUR" });
    assert.deepEqual(x, {
      ok: false,
      error: { code: "AMBIGUOUS_SEPARATOR", message: `${ERROR_MESSAGES.AMBIGUOUS_SEPARATOR} (هل تقصد 12.50 أم 1250؟)`, readings: ["12.50", "1250"] },
    });
    // Other errors carry no readings.
    const y = tryTafqit("abc");
    assert.ok(!y.ok && !("readings" in y.error));
    // cleaned follows the output language; with an invalid language, Arabic's convention.
    const z = tryTafqit("12,50", { unit: "EUR", language: "de" as Language });
    assert.ok(!z.ok && z.error.code === "INVALID_OPTION" && z.cleaned === undefined);
    const w = tryTafqit("12,50", { unit: "nope", language: "fr" });
    assert.ok(!w.ok && w.error.code === "UNKNOWN_UNIT" && w.cleaned === "12.5");
  });
});

describe("rounding to zero (B9): a warning, never a silent «صفر»", () => {
  const warn = { code: "ROUNDED_TO_ZERO", message: WARNING_MESSAGES.ROUNDED_TO_ZERO };
  test("0.004 SAR", () => {
    assert.deepEqual(tryTafqit("0.004", { unit: "SAR" }), { ok: true, result: "صفر ريال سعودي", cleaned: "0.004", warnings: [warn] });
  });
  test("every language, every subunit size", () => {
    for (const [n, unit] of [["0.004", "SAR"], ["0.0004", "KWD"], ["0.000000004", "BTC"], ["0.0049", "SAR"], ["0.00049", "OMR"]]) {
      for (const language of ["ar", "en", "fr"] as const) {
        const x = tryTafqit(language === "fr" ? n!.replace(".", ",") : n!, { unit, language });
        assert.ok(x.ok, `${n} ${unit} ${language}`);
        if (x.ok) assert.deepEqual(x.warnings, [warn], `${n} ${unit} ${language}`);
      }
    }
  });
  test("no warning when the amount is really zero or rounds to a subunit", () => {
    for (const [n, unit] of [["0", "SAR"], ["0.00", "SAR"], ["0.005", "SAR"], ["0.01", "SAR"], ["1.004", "SAR"], ["0.0005", "KWD"],
      ["0.004", "none"], ["0", "piece"], ["0.999", "SAR"]]) {
      const x = tryTafqit(n!, { unit });
      assert.ok(x.ok && !("warnings" in x), `${n} ${unit}`);
    }
  });
  test("decimals: error still rejects the extra digits", () => throwsCode(() => tafqit("0.004", { unit: "SAR", decimals: "error" }), "TOO_MANY_DECIMALS"));
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
    const bad = tryTafqit("10.125", { unit: "SAR", decimals: "error" });
    assert.equal(bad.ok, false);
    if (!bad.ok) { assert.equal(bad.error.code, "TOO_MANY_DECIMALS"); assert.equal(bad.cleaned, "10.125"); }
    const bad2 = tryTafqit("x");
    assert.ok(!bad2.ok && bad2.cleaned === undefined);
  });
  test("convert()", () => {
    assert.deepEqual(convert({ the_number: "200", unit: "riyal", hundreds_form: "مئة" }), { result: "مئتان ريال" });
    assert.deepEqual(convert({ the_number: 7 }), { result: "سبعة" });
  });
  test("units list contains none and is non-empty", () => {
    assert.ok(units.length > 10);
    assert.equal(units.find((u) => u.id === "none")?.forms, null);
  });
  test("displayDigits: whole IQD/LBP/SYP/YER amounts are written without the unused subunit", () => {
    assert.deepEqual(units.filter((u) => u.displayDigits !== undefined).map((u) => [u.id, u.displayDigits]),
      [["IQD", 0], ["SYP", 0], ["LBP", 0], ["YER", 0]]);
    for (const code of ["SAR", "AED", "USD", "EGP", "KWD", "BHD", "OMR", "JOD", "TND"]) assert.equal(findUnit(code)?.displayDigits, undefined, code);
    // Display only: the words are unchanged (no subunit for a whole amount, fils for a fraction).
    assert.equal(tafqit("1750000", { unit: "IQD" }), "مليون وسبعمائة وخمسون ألف دينار عراقي");
    assert.equal(tafqit("1750000.5", { unit: "IQD" }), "مليون وسبعمائة وخمسون ألف دينار عراقي وخمسمائة فلس");
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
  test("optional `one` form", () => {
    assert.equal(tafqit("1", { unit: "km_per_hour" }), "كيلومتر واحد في الساعة");
    assert.equal(tafqit("21", { unit: "km_per_hour" }), "واحد وعشرون كيلومتراً في الساعة");
  });
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
  test("displayDigits: 0..subunit digits, only with a subunit; never changes the words", () => {
    const sub = { singular: "a", dual: "b", plural: "c", accusative: "d", gender: "m" as const, ratio: 1000 };
    for (const d of [0, 1, 2, 3]) {
      const c = structuredClone(base);
      c.units[1]!.subunit = { ...sub };
      c.units[1]!.displayDigits = d;
      const t = createTafqit(c);
      assert.equal(t.findUnit("box")?.displayDigits, d);
      assert.equal(t.tafqit("7", { unit: "box" }), "سبعة صناديق");
    }
    for (const d of [4, -1, 1.5, "0", null]) {
      throwsCode(bad((c) => { c.units[1]!.subunit = { ...sub }; (c.units[1] as { displayDigits: unknown }).displayDigits = d; }), "INVALID_CATALOG");
    }
    throwsCode(bad((c) => { c.units[1]!.displayDigits = 0; }), "INVALID_CATALOG"); // no subunit
  });
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

describe("languages: API", () => {
  test("LANGUAGES export", () => {
    assert.deepEqual(LANGUAGES, [
      { id: "ar", label: "العربية", dir: "rtl", supportsHundredsForm: true },
      { id: "en", label: "English", dir: "ltr", supportsHundredsForm: false },
      { id: "fr", label: "Français", dir: "ltr", supportsHundredsForm: false },
    ]);
    assert.ok(Object.isFrozen(LANGUAGES) && LANGUAGES.every((l) => Object.isFrozen(l)));
  });
  test("default and explicit Arabic are identical", () => {
    for (const n of ["0", "21", "1250.75", "200000"]) {
      assert.equal(tafqit(n, { unit: "SAR", language: "ar" }), tafqit(n, { unit: "SAR" }));
      assert.equal(tafqit(n, { unit: "SAR", language: "" as Language }), tafqit(n, { unit: "SAR" }));
    }
  });
  test("invalid language", () => {
    for (const l of ["de", "AR", "english", "fr-FR", " en"]) {
      throwsCode(() => tafqit("1", { language: l as Language }), "INVALID_OPTION");
    }
    const bad = tryTafqit("1", { language: "xx" as Language });
    assert.ok(!bad.ok && bad.error.code === "INVALID_OPTION");
  });
  test("hundredsForm is ignored for en/fr but still validated", () => {
    assert.equal(tafqit("200", { language: "en", hundredsForm: "مئة" }), "Two Hundred");
    assert.equal(tafqit("200", { language: "fr", hundredsForm: "مئة" }), "deux cents");
    throwsCode(() => tafqit("1", { language: "en", hundredsForm: "x" as never }), "INVALID_OPTION");
  });
  test("convert() passes language", () => {
    assert.deepEqual(convert({ the_number: "21", unit: "EGP", language: "fr" }), { result: "vingt et une livres égyptiennes" });
    assert.deepEqual(convert({ the_number: 7, language: "en" }), { result: "Seven" });
  });
  test("shared errors and rounding apply to every language", () => {
    for (const language of ["en", "fr"] as const) {
      throwsCode(() => tafqit("2.5", { unit: "piece", language }), "FRACTION_NOT_SUPPORTED");
      throwsCode(() => tafqit("10.125", { unit: "SAR", language, decimals: "error" }), "TOO_MANY_DECIMALS");
      throwsCode(() => tafqit("-1", { language }), "NEGATIVE");
      throwsCode(() => tafqit("1", { unit: "xyz", language }), "UNKNOWN_UNIT");
      throwsCode(() => tafqit("1." + "1".repeat(16), { language }), "TOO_MANY_DECIMALS");
    }
    assert.equal(tafqit("1.995", { unit: "SAR", language: "en" }), "Two Saudi Riyals");
    assert.equal(tafqit("9.9999", { unit: "KWD", language: "fr" }), "dix dinars koweïtiens");
    assert.equal(tafqit(0.1 + 0.2, { unit: "SAR", language: "en" }), "Thirty Halalas");
    assert.equal(tafqit("0.004", { unit: "SAR", language: "fr" }), "zéro riyal saoudien");
  });
  test("prefix/suffix", () => {
    assert.equal(tafqit("5", { unit: "USD", language: "en", prefix: " Only ", suffix: "" }), "Only Five US Dollars");
  });
});

describe("languages: English", () => {
  const cases: [string, string][] = [
    ["0", "Zero"], ["7", "Seven"], ["11", "Eleven"], ["15", "Fifteen"], ["19", "Nineteen"], ["20", "Twenty"],
    ["45", "Forty-Five"], ["99", "Ninety-Nine"], ["110", "One Hundred Ten"], ["999", "Nine Hundred Ninety-Nine"],
    ["1001", "One Thousand One"], ["12000", "Twelve Thousand"], ["100000", "One Hundred Thousand"],
    ["1001001001001", "One Trillion One Billion One Million One Thousand One"],
    ["999999999999999", "Nine Hundred Ninety-Nine Trillion Nine Hundred Ninety-Nine Billion Nine Hundred Ninety-Nine Million Nine Hundred Ninety-Nine Thousand Nine Hundred Ninety-Nine"],
    ["0.5", "Zero Point Five"], ["3.125", "Three Point One Two Five"], ["7.007", "Seven Point Zero Zero Seven"],
  ];
  for (const [n, want] of cases) test(n, () => assert.equal(tafqit(n, { language: "en" }), want));
  test("counted nouns", () => {
    const en = (n: string, unit: string) => tafqit(n, { unit, language: "en" });
    assert.equal(en("0.01", "GBP"), "One Penny");
    assert.equal(en("2.02", "GBP"), "Two Pounds Sterling and Two Pence");
    assert.equal(en("0.50", "SAR"), "Fifty Halalas");
    assert.equal(en("3", "foot"), "Three Feet");
    assert.equal(en("1.001", "OMR"), "One Omani Rial and One Baisa");
    assert.equal(en("2", "km_per_hour"), "Two Kilometres per Hour");
  });
});

describe("languages: French", () => {
  const cases: [string, string][] = [
    ["0", "zéro"], ["1", "un"], ["16", "seize"], ["17", "dix-sept"], ["31", "trente et un"], ["61", "soixante et un"],
    ["70", "soixante-dix"], ["72", "soixante-douze"], ["77", "soixante-dix-sept"], ["79", "soixante-dix-neuf"],
    ["90", "quatre-vingt-dix"], ["99", "quatre-vingt-dix-neuf"], ["101", "cent un"], ["180", "cent quatre-vingts"],
    ["280", "deux cent quatre-vingts"], ["300", "trois cents"], ["310", "trois cent dix"], ["1100", "mille cent"],
    ["2001", "deux mille un"], ["21000", "vingt et un mille"], ["80000", "quatre-vingt mille"],
    ["81000", "quatre-vingt-un mille"], ["200000", "deux cent mille"], ["1000500", "un million cinq cents"],
    ["21000000", "vingt et un millions"], ["300000000", "trois cents millions"], ["1000000000", "un milliard"],
    ["1000000001", "un milliard un"], ["2000000000000", "deux billions"],
    ["999999999999999", "neuf cent quatre-vingt-dix-neuf billions neuf cent quatre-vingt-dix-neuf milliards neuf cent quatre-vingt-dix-neuf millions neuf cent quatre-vingt-dix-neuf mille neuf cent quatre-vingt-dix-neuf"],
    ["0.5", "zéro virgule cinq"], ["3.125", "trois virgule cent vingt-cinq"],
  ];
  for (const [n, want] of cases) test(n, () => assert.equal(tafqit(n, { language: "fr" }), want));
  const fr = (n: string, unit: string) => tafqit(n, { unit, language: "fr" });
  test("gender agreement on the last group only", () => {
    assert.equal(fr("1", "lira"), "une livre");
    assert.equal(fr("31", "lira"), "trente et une livres");
    assert.equal(fr("201", "lira"), "deux cent une livres");
    assert.equal(fr("1001", "EGP"), "mille une livres égyptiennes");
    assert.equal(fr("1000001", "EGP"), "un million une livres égyptiennes");
    assert.equal(fr("21000", "lira"), "vingt et un mille livres");
    assert.equal(fr("21000000", "lira"), "vingt et un millions de livres");
    assert.equal(fr("71", "hour"), "soixante et onze heures");
  });
  test("de / d' after an exact million, milliard or billion", () => {
    assert.equal(fr("1000000", "USD"), "un million de dollars américains");
    assert.equal(fr("3000000000", "hectare"), "trois milliards d'hectares");
    assert.equal(fr("200000000", "hour"), "deux cents millions d'heures");
    assert.equal(fr("1000000000000", "EUR"), "un billion d'euros");
    assert.equal(fr("1000500", "MAD"), "un million cinq cents dirhams marocains");
    assert.equal(fr("1001000", "MAD"), "un million mille dirhams marocains");
    assert.equal(fr("0.01", "BTC"), "un million de satoshis");
  });
  test("singular for 0 and 1, plural from 2", () => {
    assert.equal(fr("0", "piece"), "zéro pièce");
    assert.equal(fr("1", "piece"), "une pièce");
    assert.equal(fr("2", "roll"), "deux rouleaux");
    assert.equal(fr("0.01", "GBP"), "un penny");
  });
});

describe("languages: catalog", () => {
  const box = { singular: "صندوق", dual: "صندوقان", plural: "صناديق", accusative: "صندوقاً", gender: "m" } as const;
  const base: UnitCatalog = {
    schemaVersion: 1,
    units: [
      { id: "none", category: "none", label: "بدون وحدة", forms: null },
      {
        id: "box", category: "count", label: "صندوق", forms: box,
        i18n: { en: { singular: "Box", plural: "Boxes" }, fr: { singular: "caisse", plural: "caisses", gender: "f" } },
      },
      { id: "arOnly", category: "count", label: "كيس", forms: { ...box, singular: "كيس" } },
      {
        id: "coin", category: "currency-world", label: "عملة", forms: box,
        i18n: { en: { singular: "Coin", plural: "Coins", one: "A Single Coin" }, fr: { singular: "pièce", plural: "pièces", gender: "f", one: "une seule pièce" } },
        subunit: { ...box, ratio: 100 },
      },
    ],
  };
  test("custom catalog with i18n and `one`", () => {
    const t = createTafqit(base);
    assert.equal(t.tafqit("21", { unit: "box", language: "fr" }), "vingt et une caisses");
    assert.equal(t.tafqit("1", { unit: "coin", language: "en" }), "A Single Coin");
    assert.equal(t.tafqit("1", { unit: "coin", language: "fr" }), "une seule pièce");
    assert.equal(t.tafqit("2.5", { unit: "none", language: "en" }), "Two Point Five");
  });
  test("NO_LANGUAGE_FORMS", () => {
    const t = createTafqit(base);
    assert.equal(ERROR_MESSAGES.NO_LANGUAGE_FORMS, "لا تتوفر صيغة لهذه الوحدة باللغة المختارة.");
    throwsCode(() => t.tafqit("1", { unit: "arOnly", language: "en" }), "NO_LANGUAGE_FORMS");
    throwsCode(() => t.tafqit("1", { unit: "arOnly", language: "fr" }), "NO_LANGUAGE_FORMS");
    assert.equal(t.tafqit("3", { unit: "arOnly" }), "ثلاثة صناديق");
    // The subunit's forms are needed only when a subunit amount is written.
    assert.equal(t.tafqit("2", { unit: "coin", language: "en" }), "Two Coins");
    assert.equal(t.tafqit("2.00", { unit: "coin", language: "fr" }), "deux pièces");
    throwsCode(() => t.tafqit("2.5", { unit: "coin", language: "en" }), "NO_LANGUAGE_FORMS");
    const res = t.tryTafqit("1", { unit: "arOnly", language: "fr" });
    assert.ok(!res.ok && res.error.code === "NO_LANGUAGE_FORMS" && res.error.message.startsWith(ERROR_MESSAGES.NO_LANGUAGE_FORMS));
  });
  const bad = (mut: (c: UnitCatalog) => void) => {
    const c = structuredClone(base); mut(c); return () => createTafqit(c);
  };
  const i18n = (c: UnitCatalog) => c.units[1]!.i18n as Record<string, Record<string, unknown>>;
  test("i18n validation", () => {
    throwsCode(bad((c) => { c.units[0]!.i18n = {}; }), "INVALID_CATALOG");
    throwsCode(bad((c) => { i18n(c).de = { singular: "Kiste", plural: "Kisten" }; }), "INVALID_CATALOG");
    throwsCode(bad((c) => { i18n(c).ar = { singular: "صندوق", plural: "صناديق" }; }), "INVALID_CATALOG");
    throwsCode(bad((c) => { delete i18n(c).fr!.gender; }), "INVALID_CATALOG");
    throwsCode(bad((c) => { i18n(c).fr!.gender = "n"; }), "INVALID_CATALOG");
    throwsCode(bad((c) => { i18n(c).en!.gender = "m"; }), "INVALID_CATALOG");
    throwsCode(bad((c) => { i18n(c).en!.plural = ""; }), "INVALID_CATALOG");
    throwsCode(bad((c) => { i18n(c).en!.singular = " Box"; }), "INVALID_CATALOG");
    throwsCode(bad((c) => { i18n(c).en!.one = 1; }), "INVALID_CATALOG");
    throwsCode(bad((c) => { i18n(c).en!.plurals = "Boxes"; }), "INVALID_CATALOG");
    throwsCode(bad((c) => { c.units[1]!.i18n = [] as never; }), "INVALID_CATALOG");
    throwsCode(bad((c) => { c.units[3]!.subunit!.i18n = { fr: { singular: "x", plural: "y" } as never }; }), "INVALID_CATALOG");
  });
  test("bundled catalog: en and fr forms for every unit and subunit", () => {
    for (const u of units) {
      if (u.forms === null) { assert.equal(u.i18n, undefined); continue; }
      assert.ok(u.i18n?.en && u.i18n.fr, `${u.id}`);
      if (u.subunit) assert.ok(u.subunit.i18n?.en && u.subunit.i18n.fr, `${u.id}.subunit`);
    }
  });
});

describe("languages: every unit, hygiene", () => {
  const clean = (s: string) => !/\s{2}|^\s|\s$|--|^-|-$|\s-|-\s/.test(s);
  test("every unit converts 0/1/2/21/1000000/1000.5 in en and fr", () => {
    for (const language of ["en", "fr"] as const) {
      for (const u of units) {
        const inputs = ["0", "1", "2", "21", "1000000", ...(u.subunit || u.forms === null ? ["1000.5"] : [])];
        for (const n of inputs) {
          const s = tafqit(n, { unit: u.id, language });
          assert.ok(clean(s), `${language} ${u.id} ${n}: «${s}»`);
        }
      }
    }
  });
  test("no double spaces, trimmed, well-formed (0..20000 and samples)", () => {
    const samples: string[] = [];
    for (let i = 0; i <= 20000; i++) samples.push(String(i));
    for (let i = 1; i < 2000; i++) samples.push(String(BigInt(i) * 7919n * 1000003n));
    for (const unit of ["none", "riyal", "lira", "SAR", "hour"]) {
      for (const n of samples) {
        const en = tafqit(n, { unit, language: "en" });
        assert.ok(clean(en) && !/ and /.test(en) && /^[A-Z]/.test(en), `en ${unit} ${n}: «${en}»`);
        const fr = tafqit(n, { unit, language: "fr" });
        const ok = clean(fr)
          && !/(^|(million|milliard|billion)s? )un mille/.test(fr) // never "un mille" for 1000
          && !/ et (?!une? |une?$|onze)/.test(fr) // "et" only in 21…71
          && !/(cents|vingts) (un|une|deux|trois|quatre|cinq|six|sept|huit|neuf|dix|onze|douze|treize|quatorze|quinze|seize|vingt|trente|quarante|cinquante|soixante|cent|mille)\b/.test(fr);
        assert.ok(ok, `fr ${unit} ${n}: «${fr}»`);
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
