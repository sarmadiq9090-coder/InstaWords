import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
// @ts-ignore: plain ES module script, no type declarations
import { changelogSection } from "../scripts/changelog-section.mjs";

const read = (p: string) => readFileSync(new URL(p, import.meta.url), "utf8");

test("CHANGELOG.md has notes for the version in package.json (the release copies them)", () => {
  const { version } = JSON.parse(read("../package.json"));
  const section = changelogSection(read("../CHANGELOG.md"), version);
  assert.ok(section && section.length > 40, `CHANGELOG.md needs a "## ${version}" section`);
});

test("changelogSection takes only the lines of that version", () => {
  const text = "# Log\r\n\r\n## 2.0.0\r\n- b\r\n\r\n## 1.0.0\r\n- a\r\n";
  assert.equal(changelogSection(text, "2.0.0"), "- b");
  assert.equal(changelogSection(text, "1.0.0"), "- a");
  assert.equal(changelogSection(text, "3.0.0"), null);
});
