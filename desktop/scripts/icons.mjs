// Regenerate desktop/src-tauri/icons from site/icon.svg.
//   npm i --no-save playwright && npx playwright install chromium   (once)
//   node desktop/scripts/icons.mjs
// Renders the SVG at 1024x1024 (transparent corners) with headless Chromium, then runs
// `tauri icon` to produce the .ico / .icns / PNG set. Mobile icons are removed (desktop only).
import { chromium } from "playwright";
import { execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const iconsDir = join(root, "desktop", "src-tauri", "icons");
const tmp = mkdtempSync(join(tmpdir(), "instawords-icon-"));
const png = join(tmp, "icon-1024.png");

const svg = readFileSync(join(root, "site", "icon.svg"), "utf8");
const browser = await chromium.launch();
try {
  const page = await browser.newPage({ viewport: { width: 1024, height: 1024 }, deviceScaleFactor: 1 });
  await page.setContent(
    `<!doctype html><style>html,body{margin:0;background:transparent}svg{display:block;width:1024px;height:1024px}</style>${svg}`,
  );
  await page.locator("svg").screenshot({ path: png, omitBackground: true });
} finally {
  await browser.close();
}

execFileSync("npx", ["tauri", "icon", png, "-o", iconsDir], { cwd: root, stdio: "inherit", shell: process.platform === "win32" });
for (const d of ["android", "ios"]) rmSync(join(iconsDir, d), { recursive: true, force: true });
rmSync(tmp, { recursive: true, force: true });
console.log(`icons written to ${iconsDir}`);
