// Prints the CHANGELOG.md section of one version (the lines under "## <version>" up to the next "## "),
// for the release notes. Exits 1 when the version has no section, so a release cannot ship without notes.
//
//   node scripts/changelog-section.mjs 2.2.0
import { readFileSync } from "node:fs";

export function changelogSection(text, version) {
  const lines = text.replace(/\r\n/g, "\n").split("\n");
  const start = lines.findIndex((l) => l.trim() === `## ${version}`);
  if (start < 0) return null;
  let end = lines.findIndex((l, i) => i > start && /^## /.test(l));
  if (end < 0) end = lines.length;
  return lines.slice(start + 1, end).join("\n").trim();
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const version = process.argv[2];
  const section = version && changelogSection(readFileSync(new URL("../CHANGELOG.md", import.meta.url), "utf8"), version);
  if (!section) {
    console.error(`CHANGELOG.md has no "## ${version}" section.`);
    process.exit(1);
  }
  console.log(section);
}
