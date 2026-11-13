import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { tafqit, tryTafqit, normalizeNumber, TafqitError } from "../src/index.ts";
import type { HundredsForm, Language, TafqitErrorCode, TafqitWarningCode } from "../src/index.ts";

interface GoldenCase {
  id: string; input: string; unit: string; expected: string;
  /** The number used; absent for error cases. */
  cleaned?: string;
  /** Error cases: the code, the readings, and `expected` is the full message (what the VBA returns). */
  error?: TafqitErrorCode; readings?: string[];
  warnings?: TafqitWarningCode[];
  options?: { hundredsForm?: HundredsForm; prefix?: string; suffix?: string; language?: Language };
}
const golden = JSON.parse(readFileSync(new URL("./golden/cases.json", import.meta.url), "utf8")) as {
  defaults: { hundredsForm: HundredsForm; prefix: string; suffix: string }; cases: GoldenCase[];
};

for (const c of golden.cases) {
  test(`${c.id}: ${c.input} [${c.unit}]${c.options?.language ? ` (${c.options.language})` : ""}`, () => {
    const opts = { ...golden.defaults, ...c.options, unit: c.unit, language: c.options?.language };
    const r = tryTafqit(c.input, opts);
    if (c.error) {
      assert.equal(c.cleaned, undefined);
      assert.throws(() => tafqit(c.input, opts), (e: unknown) => {
        assert.ok(e instanceof TafqitError);
        assert.equal(e.code, c.error);
        assert.equal(e.message, c.expected);
        assert.deepEqual(e.readings, c.readings);
        return true;
      });
      assert.ok(!r.ok);
      if (!r.ok) {
        assert.equal(r.error.code, c.error);
        assert.equal(r.error.message, c.expected);
        assert.deepEqual(r.error.readings, c.readings);
      }
      assert.throws(() => normalizeNumber(c.input, c.options?.language), { code: c.error });
      return;
    }
    assert.equal(tafqit(c.input, opts), c.expected);
    assert.equal(normalizeNumber(c.input, c.options?.language).cleaned, c.cleaned);
    assert.deepEqual(r, {
      ok: true, result: c.expected, cleaned: c.cleaned,
      ...(c.warnings ? { warnings: c.warnings.map((code) => ({ code, message: r.ok ? r.warnings?.find((w) => w.code === code)?.message : "" })) } : {}),
    });
  });
}

test("golden cases cover the separator rules: French decimal comma, AMBIGUOUS_SEPARATOR, ROUNDED_TO_ZERO", () => {
  const ids = (p: (c: GoldenCase) => boolean) => golden.cases.filter(p).length;
  assert.ok(ids((c) => c.options?.language === "fr" && c.input.includes(",") && !c.error) >= 8);
  assert.ok(ids((c) => c.error === "AMBIGUOUS_SEPARATOR") >= 10);
  assert.ok(ids((c) => c.warnings?.includes("ROUNDED_TO_ZERO") ?? false) >= 3);
});
