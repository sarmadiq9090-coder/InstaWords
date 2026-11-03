import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { tafqit, normalizeNumber } from "../src/index.ts";
import type { HundredsForm, Language } from "../src/index.ts";

interface GoldenCase {
  id: string; input: string; unit: string; cleaned: string; expected: string;
  options?: { hundredsForm?: HundredsForm; prefix?: string; suffix?: string; language?: Language };
}
const golden = JSON.parse(readFileSync(new URL("./golden/cases.json", import.meta.url), "utf8")) as {
  defaults: { hundredsForm: HundredsForm; prefix: string; suffix: string }; cases: GoldenCase[];
};

for (const c of golden.cases) {
  test(`${c.id}: ${c.input} [${c.unit}]${c.options?.language ? ` (${c.options.language})` : ""}`, () => {
    const opts = { ...golden.defaults, ...c.options, unit: c.unit, language: c.options?.language };
    assert.equal(tafqit(c.input, opts), c.expected);
    assert.equal(normalizeNumber(c.input).cleaned, c.cleaned);
  });
}
