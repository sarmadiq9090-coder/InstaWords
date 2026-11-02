// Build the deployable static site into _site/ (run via `npm run build:site`, after `npm run build`).
//   site/*            → _site/
//   dist/**/*.js      → _site/lib/
//   data/units.json   → _site/units.json
//   vba/Tafqit.bas    → _site/Tafqit.bas (when present)
// Then stamps a content-hash cache version and the precache list into _site/sw.js.
import { createHash } from "node:crypto";
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from "node:fs";
import { dirname, join, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const out = join(root, "_site");
const at = (...p) => join(root, ...p);

function walk(dir) {
  const files = [];
  for (const name of readdirSync(dir)) {
    const p = join(dir, name);
    if (statSync(p).isDirectory()) files.push(...walk(p));
    else files.push(p);
  }
  return files;
}

const fail = (msg) => {
  console.error(`build-site: ${msg}`);
  process.exit(1);
};

if (!existsSync(at("dist", "index.js"))) fail("dist/index.js is missing; run `npm run build` first.");

rmSync(out, { recursive: true, force: true });
mkdirSync(out, { recursive: true });

// 1. Site sources.
cpSync(at("site"), out, { recursive: true });

// 2. Core library: ES modules only (no .d.ts / maps).
for (const file of walk(at("dist"))) {
  if (!file.endsWith(".js")) continue;
  const dest = join(out, "lib", relative(at("dist"), file));
  mkdirSync(dirname(dest), { recursive: true });
  cpSync(file, dest);
}

// 3. Units catalog, fetched at runtime by the pages.
cpSync(at("data", "units.json"), join(out, "units.json"));

// 4. Excel VBA module (built separately; optional here).
// Without the module the Excel page would offer a broken download, so it is left out of the
// build and its navigation links are removed until vba/Tafqit.bas exists.
if (existsSync(at("vba", "Tafqit.bas"))) cpSync(at("vba", "Tafqit.bas"), join(out, "Tafqit.bas"));
else {
  console.warn("build-site: warning: vba/Tafqit.bas not found; the Excel page is left out of this build.");
  rmSync(join(out, "excel.html"));
  for (const name of readdirSync(out)) {
    if (!name.endsWith(".html")) continue;
    const p = join(out, name);
    const html = readFileSync(p, "utf8");
    const stripped = html.replace(/^[ \t]*<li><a href="excel\.html"[^\n]*<\/li>\r?\n/gm, "");
    if (stripped.includes('href="excel.html"')) fail(`${name}: could not remove the link to excel.html`);
    writeFileSync(p, stripped);
  }
}

// 5. GitHub Pages: serve files as-is.
writeFileSync(join(out, ".nojekyll"), "");

// 6. Service worker: precache everything; version = hash of all precached content.
const swPath = join(out, "sw.js");
const assets = walk(out)
  .map((f) => relative(out, f).split(sep).join("/"))
  .filter((p) => p !== "sw.js" && !p.split("/").some((part) => part.startsWith(".")))
  .sort();
const hash = createHash("sha256");
for (const p of assets) hash.update(p).update("\0").update(readFileSync(join(out, p))).update("\0");
const version = hash.digest("hex").slice(0, 16);

let sw = readFileSync(swPath, "utf8");
const before = sw;
sw = sw.replace(/^const VERSION = .*;$/m, () => `const VERSION = ${JSON.stringify(version)};`);
sw = sw.replace(/^const ASSETS = .*;$/m, () => `const ASSETS = ${JSON.stringify(["./", ...assets], null, 2)};`);
if (sw === before || !sw.includes(version)) fail("could not stamp VERSION/ASSETS into sw.js");
writeFileSync(swPath, sw);

console.log(`build-site: _site/ ready (${assets.length + 1} files precached, cache version ${version})`);
