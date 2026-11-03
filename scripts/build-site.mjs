// Build the deployable static site into _site/ (run via `npm run build:site`, after `npm run build`).
//   site/*            → _site/   (Arabic pages at the root, English pages in en/, sharing assets/)
//   dist/**/*.js      → _site/lib/
//   data/units.json   → _site/units.json
//   LICENSE           → _site/LICENSE.txt
//   vba/Tafqit.bas    → _site/Tafqit.bas (when present)
//                     → _site/InstaWords.xlam (Excel add-in, built by scripts/build-xlam.mjs)
// Writes the fallback release from site/release-fallback.json into the pages' static download
// links, their version labels, the JSON-LD downloadUrl and assets/release.js.
// Then stamps a content-hash cache version and the precache list into _site/sw.js.
import { createHash } from "node:crypto";
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from "node:fs";
import { dirname, join, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { readFallback } from "./lib/release.mjs";
import { buildXlam } from "./build-xlam.mjs";

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

/** Every page of every interface language, as [name relative to _site/, absolute path]. */
const pages = () =>
  walk(out)
    .filter((f) => f.endsWith(".html"))
    .map((f) => [relative(out, f).split(sep).join("/"), f]);
/** Interface languages: the root holds Arabic, each of these sub-folders another language. */
const LANG_DIRS = ["en"];

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

// 3b. Licence, linked from the footer so it opens on the site itself.
cpSync(at("LICENSE"), join(out, "LICENSE.txt"));

// 4. Excel VBA module (built separately; optional here).
// Without the module the Excel page would offer a broken download, so it is left out of the
// build and its navigation links are removed until vba/Tafqit.bas exists.
if (existsSync(at("vba", "Tafqit.bas"))) {
  cpSync(at("vba", "Tafqit.bas"), join(out, "Tafqit.bas"));
  // The add-in is served by the site itself, so its download link works before any release has it.
  const { version } = JSON.parse(readFileSync(at("package.json"), "utf8"));
  writeFileSync(join(out, "InstaWords.xlam"), buildXlam(readFileSync(at("vba", "Tafqit.bas"), "latin1"), { version }));
} else {
  console.warn("build-site: warning: vba/Tafqit.bas not found; the Excel page is left out of this build.");
  for (const dir of ["", ...LANG_DIRS]) rmSync(join(out, dir, "excel.html"), { force: true });
  for (const [name, p] of pages()) {
    const html = readFileSync(p, "utf8");
    // Remove one-line list items linking to the page, and any line marked data-excel-only.
    const stripped = html
      .replace(/^[ \t]*<li><a href="excel\.html"[^\n]*<\/li>\r?\n/gm, "")
      .replace(/^[^\n]*data-excel-only[^\n]*\r?\n/gm, "");
    if (stripped.includes('href="excel.html"')) fail(`${name}: could not remove the link to excel.html`);
    writeFileSync(p, stripped);
  }
}

// 4b. Fallback release (site/release-fallback.json, the single source): every installer/portable
// link to a release file, every version label in a download button, and release.js FALLBACK_DATA.
let fallback;
try {
  fallback = readFallback(at("site", "release-fallback.json"));
} catch (err) {
  fail(err.message);
}
{
  const SETUP_URL_RE = /https:\/\/github\.com\/sarmadiq9090-coder\/tafket\/releases\/download\/v[\d.]+\/InstaWords_[\d.]+_x64-setup\.exe/g;
  const PORTABLE_URL_RE = /https:\/\/github\.com\/sarmadiq9090-coder\/tafket\/releases\/download\/v[\d.]+\/InstaWords_[\d.]+_x64-portable\.zip/g;
  const VERSION_LABEL_RE = /(<span\b[^>]*\bdata-version\b[^>]*>)v[^<]*(<\/span>)/g;
  for (const [name, p] of pages()) {
    let html = readFileSync(p, "utf8");
    const links = html.match(SETUP_URL_RE)?.length ?? 0;
    html = html
      .replace(SETUP_URL_RE, () => fallback.setup.url)
      .replace(PORTABLE_URL_RE, () => fallback.portable?.url ?? fallback.setup.url)
      .replace(VERSION_LABEL_RE, (_, open, close) => `${open}v${fallback.version}${close}`);
    const buttons = [...html.matchAll(/<a\b[^>]*\bdata-windows-download\b[^>]*>/g)];
    for (const [tag] of buttons) {
      if (!tag.includes(`href="${fallback.setup.url}"`)) fail(`${name}: a data-windows-download link does not point at the fallback installer: ${tag}`);
    }
    if (buttons.length && !links) fail(`${name}: no installer link to update`);
    writeFileSync(p, html);
  }
  const jsPath = join(out, "assets", "release.js");
  const js = readFileSync(jsPath, "utf8");
  const MARK = "/* @release-fallback */ null";
  if (js.split(MARK).length !== 2) fail(`assets/release.js: expected the marker \`${MARK}\` exactly once`);
  writeFileSync(jsPath, js.replace(MARK, () => `/* @release-fallback */ ${JSON.stringify(fallback)}`));
  for (const dir of ["", ...LANG_DIRS]) {
    const name = join(dir, "index.html");
    if (!readFileSync(join(out, name), "utf8").includes(`"downloadUrl": "${fallback.setup.url}"`)) {
      fail(`${name}: JSON-LD downloadUrl does not name the fallback installer`);
    }
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
// Each language's folder URL ("./", "en/") is precached too, so /instawords/en/ opens offline.
const folders = ["./", ...LANG_DIRS.filter((d) => assets.includes(`${d}/index.html`)).map((d) => `${d}/`)];
sw = sw.replace(/^const ASSETS = .*;$/m, () => `const ASSETS = ${JSON.stringify([...folders, ...assets], null, 2)};`);
if (sw === before || !sw.includes(version)) fail("could not stamp VERSION/ASSETS into sw.js");
writeFileSync(swPath, sw);

console.log(`build-site: _site/ ready (fallback release v${fallback.version}, ${assets.length + folders.length} files precached, cache version ${version})`);
