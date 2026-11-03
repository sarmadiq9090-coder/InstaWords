// End-to-end smoke test of the built site in _site/ (run via `npm run test:site`).
// Serves _site/ under the sub-path /instawords/ (like the real deployment) and drives Chromium with Playwright.
// Arabic pages are at /instawords/, English pages at /instawords/en/ (same assets, interface strings
// in site/assets/i18n.js).
// Set TAFKET_SCREENSHOTS=<dir> to also save mobile (375px) and desktop (1280px) screenshots, and the
// English pages at 390px and 1366px in <dir>/en/.
import { createServer } from "node:http";
import { createRequire } from "node:module";
import { existsSync, mkdirSync, readFileSync, statSync } from "node:fs";
import { extname, join, normalize, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { readFallback } from "../../scripts/lib/release.mjs";

const require = createRequire(import.meta.url);
let chromium;
try {
  ({ chromium } = require("playwright"));
} catch {
  ({ chromium } = await import("playwright"));
}

const root = fileURLToPath(new URL("../../_site/", import.meta.url));
if (!existsSync(join(root, "index.html"))) {
  console.error("smoke: _site/ not found; run `npm run build:site` first.");
  process.exit(1);
}
const core = await import(new URL("../../dist/index.js", import.meta.url));
const { tafqit } = core;
// Output languages: real ones when the core exports LANGUAGES; otherwise the picker UI is checked
// against a stub export injected into lib/index.js (Arabic output, layout and behaviour only).
const REAL_LANGUAGES = Array.isArray(core.LANGUAGES) && core.LANGUAGES.length > 1 ? core.LANGUAGES : null;
const STUB_LANGUAGES = [
  { id: "ar", label: "العربية", dir: "rtl", supportsHundredsForm: true },
  { id: "en", label: "English", dir: "ltr", supportsHundredsForm: false },
  { id: "fr", label: "Français", dir: "ltr", supportsHundredsForm: false },
];
const REPO_URL = "https://github.com/sarmadiq9090-coder/tafket";
const RELEASE_API = "https://api.github.com/repos/sarmadiq9090-coder/tafket/releases/latest";
// Direct installer used without JavaScript / when GitHub is unreachable: site/release-fallback.json,
// which scripts/build-site.mjs writes into the pages and into release.js FALLBACK.
const FB = readFallback();
const STATIC_EXE = FB.setup.url;
const FB_V = `v${FB.version}`;
// GitHub API fixture: what the buttons and the #download dialog must show when the API answers.
// Its version is always newer than the fallback (next minor), so the two can never be confused.
const FV = `${FB.version.split(".")[0]}.${Number(FB.version.split(".")[1]) + 1}.0`;
const FIXTURE_EXE = `${REPO_URL}/releases/download/v${FV}/InstaWords_${FV}_x64-setup.exe`;
const FIXTURE_ZIP = `${REPO_URL}/releases/download/v${FV}/InstaWords_${FV}_x64-portable.zip`;
const FIXTURE_SHA = "ab".repeat(32);
const FIXTURE = {
  tag_name: `v${FV}`,
  name: `InstaWords v${FV}`,
  html_url: `${REPO_URL}/releases/tag/v${FV}`,
  published_at: "2026-12-01T10:00:00Z",
  body: [
    `## InstaWords — تفقيط v${FV}`,
    "",
    '<div dir="rtl">',
    "",
    "**العربية**",
    "",
    `- **\`InstaWords_${FV}_x64-setup.exe\`**: المثبّت (موصى به).`,
    '- تحسين التصدير <img src=x onerror="window.__pwned=1"> إلى CSV.',
    "",
    "<script>window.__pwned=2</script>",
    "",
    "### Fixes",
    "1. First fix",
    "2. Second fix",
    "",
    "</div>",
  ].join("\r\n"),
  assets: [
    { name: `InstaWords_${FV}_x64-setup.exe.blockmap`, size: 10, digest: null, browser_download_url: `${FIXTURE_EXE}.blockmap` },
    { name: `InstaWords_${FV}_x64-portable.zip`, size: 600000, digest: `sha256:${"cd".repeat(32)}`, browser_download_url: FIXTURE_ZIP },
    { name: `InstaWords_${FV}_x64-setup.exe`, size: 2621440, digest: `sha256:${FIXTURE_SHA}`, browser_download_url: FIXTURE_EXE },
  ],
};
/** Answer the GitHub release API with the fixture ("ok") or fail it like an unreachable network ("down"). */
async function mockReleaseApi(context, mode = "ok") {
  await context.route("https://api.github.com/**", (route) =>
    mode === "ok"
      ? route.fulfill({ status: 200, contentType: "application/json; charset=utf-8", headers: { "access-control-allow-origin": "*" }, body: JSON.stringify(FIXTURE) })
      : route.abort("internetdisconnected"),
  );
}
async function newContext(options = {}, api = "ok") {
  const c = await browser.newContext(options);
  await mockReleaseApi(c, api);
  return c;
}

const PREFIX = "/instawords/";
/** Every page, Arabic (root) and English (en/). */
const PAGES = ["index.html", "bulk.html", "excel.html", "en/index.html", "en/bulk.html", "en/excel.html"];
const SITE_URL = "https://fastkiwi.app/instawords/";
/** Arabic letters (not Arabic-Indic digits or the Arabic decimal separator, which English pages mention as input). */
const ARABIC_LETTERS = /[\u0621-\u064A\u066E-\u06D3\u06FA-\u06FF\u0750-\u077F\uFB50-\uFDFF\uFE70-\uFEFC]/;
const MIME = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".webmanifest": "application/manifest+json; charset=utf-8",
  ".svg": "image/svg+xml",
  ".bas": "text/plain; charset=utf-8",
  ".txt": "text/plain; charset=utf-8",
  ".woff2": "font/woff2",
};

const server = createServer((req, res) => {
  const path = decodeURIComponent(new URL(req.url, "http://x").pathname);
  if (path === PREFIX.slice(0, -1)) {
    res.writeHead(301, { Location: PREFIX }).end();
    return;
  }
  if (!path.startsWith(PREFIX)) {
    res.writeHead(404).end("outside sub-path");
    return;
  }
  let file = normalize(join(root, path.slice(PREFIX.length)));
  if (!file.startsWith(root.endsWith(sep) ? root : root + sep) && file !== root) {
    res.writeHead(403).end();
    return;
  }
  if (existsSync(file) && statSync(file).isDirectory()) file = join(file, "index.html");
  if (!existsSync(file)) {
    res.writeHead(404).end("not found");
    return;
  }
  res.writeHead(200, { "Content-Type": MIME[extname(file)] ?? "application/octet-stream", "Cache-Control": "no-cache" });
  res.end(readFileSync(file));
});
await new Promise((r) => server.listen(0, "127.0.0.1", r));
const BASE = `http://127.0.0.1:${server.address().port}${PREFIX}`;

/* ---------- tiny assertion helpers ---------- */

const failures = [];
let passed = 0;
function check(name, ok, detail = "") {
  if (ok) {
    passed++;
    console.log(`  ok   ${name}`);
  } else {
    failures.push(`${name}${detail ? ` — ${detail}` : ""}`);
    console.log(`  FAIL ${name}${detail ? ` — ${detail}` : ""}`);
  }
}
async function waitText(page, selector, expected, name, timeout = 5000) {
  try {
    await page.waitForFunction(
      ([s, e]) => document.querySelector(s)?.textContent.trim() === e,
      [selector, expected],
      { timeout },
    );
    check(name, true);
  } catch {
    const actual = await page.locator(selector).first().textContent().catch(() => "<missing>");
    check(name, false, `expected «${expected}», got «${actual?.trim()}»`);
  }
}

const consoleErrors = [];
const externalRequests = [];
function watch(page) {
  page.on("request", (r) => {
    const url = r.url();
    // The one allowed request outside the site: the latest Windows release (mocked in tests).
    if (!url.startsWith(new URL(BASE).origin) && !/^(blob|data):/.test(url) && url !== RELEASE_API) externalRequests.push(url);
  });
  page.on("console", (m) => {
    if (m.type() === "error" && !(m.location()?.url ?? "").startsWith("https://api.github.com/")) consoleErrors.push(`${page.url()}: ${m.text()}`);
  });
  page.on("pageerror", (e) => consoleErrors.push(`${page.url()}: ${e.message}`));
  return page;
}
const ready = (page) => page.waitForSelector("html[data-ready]", { timeout: 10000 });
/** Wait until the dialog `sel` is closed and its #hash cleared. */
function closed(page, sel) {
  return page.waitForFunction((s) => !document.querySelector(s)?.open && !location.hash, sel, { timeout: 5000 }).catch(() => {});
}
const rowCount = (page) => page.locator("#rows tr:not(.empty-row)").count();

const browser = await chromium.launch();
try {
  /* ---------- single page ---------- */
  console.log("single page");
  const ctx = await newContext({ permissions: ["clipboard-read", "clipboard-write"], acceptDownloads: true });
  const page = watch(await ctx.newPage());
  await page.goto(BASE);
  await ready(page);

  check("lang/dir", (await page.getAttribute("html", "lang")) === "ar" && (await page.getAttribute("html", "dir")) === "rtl");
  check("title names InstaWords and تفقيط", /InstaWords/.test(await page.title()) && (await page.title()).includes("تفقيط"), await page.title());
  check("header product name", (await page.textContent(".product")).includes("InstaWords"));
  await page.waitForSelector("html[data-release]", { timeout: 10000 }).catch(() => {});
  check("latest release resolved from the GitHub API", (await page.getAttribute("html", "data-release")) === "api", String(await page.getAttribute("html", "data-release")));
  const winLinks = await page.$$eval("[data-windows-download]", (as) => as.map((a) => ({ href: a.href, text: a.textContent })));
  check("Windows download links (hero, section, FAQ, footer) → direct installer of the latest release", winLinks.length >= 4 && winLinks.every((l) => l.href === FIXTURE_EXE), winLinks.map((l) => l.href).join(", "));
  check("Windows download links show the latest version", winLinks.every((l) => l.text.includes(`v${FV}`)), winLinks.map((l) => l.text.trim()).join(" | "));
  await releaseDialogChecks(page);
  check("Windows section says it works offline on Windows 10/11", /ويندوز 10 و11/.test(await page.textContent("#windows")));
  await page.evaluate(() => document.fonts.ready);
  check("self-hosted Plex Arabic font loaded", await page.evaluate(() => document.fonts.check('700 16px "Plex Arabic"', "تفقيط")));
  const ld = await page.$$eval('script[type="application/ld+json"]', (els) => els.map((e) => e.textContent));
  let app;
  try {
    app = ld.flatMap((t) => JSON.parse(t)["@graph"] ?? []).find((n) => n["@type"] === "WebApplication");
  } catch {}
  check("JSON-LD WebApplication published by FastKiwi", app?.publisher?.["@id"] === "https://fastkiwi.app/#org" && app?.url === "https://fastkiwi.app/instawords/");
  check("Open Graph image", (await page.getAttribute('meta[property="og:image"]', "content")) === "https://fastkiwi.app/img/social-ar.png");
  const examples = await page.$$eval("[data-example]", (els) => els.map((e) => ({ n: e.dataset.n, unit: e.dataset.unit, text: e.textContent.trim() })));
  const wrong = examples.filter((e) => e.text !== tafqit(e.n, { unit: e.unit }));
  check(`worked examples match the engine (${examples.length})`, examples.length >= 6 && wrong.length === 0, wrong.map((e) => `${e.n} ${e.unit}: «${e.text}» ≠ «${tafqit(e.n, { unit: e.unit })}»`).join("; "));
  const faqs = await page.$$eval(".faq details summary", (els) => els.map((e) => e.textContent));
  check("FAQ entries", faqs.length >= 6, faqs.join(" | "));
  const groups = await page.$$eval("#unit optgroup", (gs) => gs.map((g) => g.label));
  check("unit optgroups", groups.includes("بدون وحدة") && groups.includes("عملات عربية") && groups.length >= 13, groups.join("، "));

  await page.selectOption("#unit", "SAR");
  await page.fill("#number", "1250.75");
  await waitText(page, "#result", "ألف ومائتان وخمسون ريالاً سعودياً وخمس وسبعون هللة", "1250.75 SAR");
  await waitText(page, "#cleaned", "1,250.75", "cleaned number shown with thousands separators");

  await page.fill("#number", "1234567.5");
  await waitText(page, "#cleaned", "1,234,567.5", "thousands grouping");
  await page.fill("#number", "١٢٣");
  await waitText(page, "#result", "مائة وثلاثة وعشرون ريالاً سعودياً", "Arabic-Indic digits");
  await waitText(page, "#cleaned", "123", "Indic cleaned to 123");

  await page.fill("#number", "10.125");
  await waitText(page, "#result", "عشرة ريالات سعودية وثلاث عشرة هللة", "10.125 SAR rounds by default");
  await page.check('input[name="decimals"][value="error"]');
  await page.waitForTimeout(50);
  check("decimals=error shows error", (await page.getAttribute("#result", "class")).includes("is-error"), await page.textContent("#result"));
  await page.check('input[name="decimals"][value="round"]');

  await page.fill("#number", "abc");
  await page.waitForTimeout(50);
  const errText = await page.textContent("#result");
  check("invalid input shows Arabic error", (await page.getAttribute("#result", "class")).includes("is-error") && errText.includes("صيغة الرقم غير صحيحة"), errText);
  check("copy disabled on error", await page.isDisabled("#copy"));
  check("no stale cleaned number on invalid input", !(await page.isVisible("#cleaned-box")));

  await page.selectOption("#unit", "piece");
  await page.fill("#number", "2.5");
  await waitText(page, "#result", tryMessage("2.5", { unit: "piece" }), "fraction with piece → FRACTION_NOT_SUPPORTED");
  await page.selectOption("#unit", "SAR");

  await page.fill("#number", "300");
  await waitText(page, "#result", "ثلاثمائة ريال سعودي", "hundreds مائة (default)");
  await page.check('input[name="hundreds"][value="مئة"]');
  await waitText(page, "#result", "ثلاثمئة ريال سعودي", "hundreds مئة option");

  await page.check('input[name="hundreds"][value="مائة"]');
  await page.fill("#number", "2500");
  await page.click('[data-preset="only"]');
  await waitText(page, "#result", "فقط ألفان وخمسمائة ريال سعودي لا غير", "فقط … لا غير preset");

  await page.click("#copy");
  await page.waitForTimeout(100);
  const clip = await page.evaluate(() => navigator.clipboard.readText());
  check("copy to clipboard", clip === "فقط ألفان وخمسمائة ريال سعودي لا غير", clip);
  check("copy feedback", (await page.textContent("#copy")).includes("تم النسخ"));

  // Keyboard shortcuts and the clear button.
  await page.click('[data-preset="none"]');
  await page.fill("#number", "75");
  await page.focus("h1");
  await page.evaluate(() => document.activeElement?.blur());
  await page.keyboard.press("/");
  check('"/" focuses the number field', await page.evaluate(() => document.activeElement?.id === "number"));
  await page.evaluate(() => navigator.clipboard.writeText(""));
  await page.keyboard.press("Control+Enter");
  await page.waitForTimeout(100);
  check("Ctrl+Enter copies the result", (await page.evaluate(() => navigator.clipboard.readText())) === tafqit("75", { unit: "SAR" }));
  check("clear button shown with input", await page.isVisible("#clear-number"));
  await page.click("#clear-number");
  check("clear button empties the field", (await page.inputValue("#number")) === "" && (await page.getAttribute("#result", "class")).includes("is-empty") && !(await page.isVisible("#clear-number")));
  await page.click('[data-sample="1250.75"]');
  await waitText(page, "#result", tafqit("1250.75", { unit: "SAR" }), "sample chip fills the converter");
  await page.fill("#number", "2500");
  await page.click('[data-preset="only"]');

  // Preferences survive a reload.
  await page.check('input[name="hundreds"][value="مئة"]');
  await page.selectOption("#unit", "KWD");
  await page.reload();
  await ready(page);
  check("prefs remembered", (await page.inputValue("#unit")) === "KWD" && (await page.isChecked('input[name="hundreds"][value="مئة"]')) && (await page.inputValue("#prefix")) === "فقط");
  await page.click('[data-preset="none"]');
  await page.check('input[name="hundreds"][value="مائة"]');

  // Deep link.
  await page.goto(`${BASE}?n=${encodeURIComponent("5000")}&unit=USD`);
  await ready(page);
  await waitText(page, "#result", tafqit("5000", { unit: "USD" }), "deep link ?n=&unit=");

  /* ---------- language picker ---------- */
  console.log(`language picker (${REAL_LANGUAGES ? "core LANGUAGES: " + REAL_LANGUAGES.map((l) => l.id).join("/") : "core has no LANGUAGES yet: UI checked with a stub, output checks skipped"})`);
  check("language picker present", (await page.locator("[data-language-field]").count()) === 1);
  await languageChecks();

  /* ---------- English interface (/instawords/en/) ---------- */
  await englishChecks();

  /* ---------- bulk page ---------- */
  console.log("bulk page");
  await page.goto(`${BASE}bulk.html`);
  await ready(page);
  await page.selectOption("#unit", "SAR");
  const lines = Array.from({ length: 25 }, (_, i) => String((i + 1) * 111.5));
  lines[7] = "abc";
  await page.fill("#numbers", lines.join("\n"));
  await page.waitForFunction(() => document.querySelector("#rows")?.dataset.rendered === "25", null, { timeout: 5000 }).catch(() => {});
  check("25 lines → 25 rows", (await rowCount(page)) === 25, String(await rowCount(page)));
  check("counts ok/errors", (await page.textContent("#count-ok")) === "24" && (await page.textContent("#count-err")) === "1");
  const row1 = await page.textContent("#rows tr:nth-child(1) td.c-text");
  check("row text", row1 === tafqit("111.5", { unit: "SAR" }), row1);
  check("error shown in its row", (await page.getAttribute("#rows tr:nth-child(8)", "class"))?.includes("is-error"));

  await page.click("#copy-all");
  await page.waitForTimeout(100);
  const all = (await page.evaluate(() => navigator.clipboard.readText())).split("\n");
  check("copy all: one line per row", all.length === 25 && all[0] === row1 && all[7] === "", `${all.length} lines`);

  await page.setInputFiles("#csv-file", {
    name: "amounts.csv",
    mimeType: "text/csv",
    buffer: Buffer.from('﻿المبلغ\r\n1500\r\n"2,750.5"\r\n٣٠٠\r\n', "utf8"),
  });
  await page.waitForFunction(() => document.querySelector("#rows")?.dataset.rendered === "3", null, { timeout: 5000 }).catch(() => {});
  check("CSV import (header skipped) → 3 rows", (await rowCount(page)) === 3, String(await rowCount(page)));
  check("CSV values in textarea", (await page.inputValue("#numbers")) === "1500\n2,750.5\n٣٠٠");

  const [download] = await Promise.all([page.waitForEvent("download"), page.click("#export")]);
  const bytes = readFileSync(await download.path());
  const csv = bytes.toString("utf8");
  check("export filename", download.suggestedFilename() === "tafqit.csv");
  check("export has UTF-8 BOM", bytes[0] === 0xef && bytes[1] === 0xbb && bytes[2] === 0xbf);
  check("export has header + Arabic", csv.includes("الرقم,الرقم بعد التنقية,التفقيط,الخطأ") && csv.includes(tafqit("2750.5", { unit: "SAR" })));

  // Large input stays responsive: 10,000 lines.
  const big = Array.from({ length: 10000 }, (_, i) => `${i + 1}.${i % 100}`).join("\n");
  const t0 = Date.now();
  // Set the value as a paste would (one input event), rather than typing 10,000 lines.
  await page.$eval("#numbers", (el, v) => {
    el.value = v;
    el.dispatchEvent(new Event("input", { bubbles: true }));
  }, big);
  await page.waitForFunction(() => document.querySelector("#rows")?.dataset.rendered === "10000", null, { timeout: 20000 }).catch(() => {});
  const ms = Date.now() - t0;
  check("10,000 lines rendered", (await rowCount(page)) === 10000, `${await rowCount(page)} rows in ${ms} ms`);
  console.log(`       (10,000 rows in ${ms} ms)`);
  const lag = await page.evaluate(() => new Promise((r) => { const s = performance.now(); setTimeout(() => r(performance.now() - s), 0); }));
  check("main thread free after big render", lag < 200, `${lag.toFixed(0)} ms`);

  /* ---------- excel page ---------- */
  console.log("excel page");
  await page.goto(`${BASE}excel.html`);
  await page.waitForLoadState("load");
  check("Tafqit.bas link", (await page.getAttribute('a[href="Tafqit.bas"]', "download")) === "Tafqit.bas");
  if (existsSync(join(root, "Tafqit.bas"))) {
    const bas = await page.evaluate(async () => {
      const r = await fetch("Tafqit.bas");
      return { ok: r.ok, text: await r.text() };
    });
    check("Tafqit.bas downloadable", bas.ok && /Function TAFQIT\(/i.test(bas.text));
  } else {
    console.log("  skip Tafqit.bas not built");
  }
  const xlamLinks = await page.$$eval('a[href="InstaWords.xlam"]', (as) => as.map((a) => ({ href: a.href, download: a.getAttribute("download") })));
  check("InstaWords.xlam links are direct same-site downloads", xlamLinks.length >= 2 && xlamLinks.every((l) => l.download === "InstaWords.xlam" && l.href === `${BASE}InstaWords.xlam`), JSON.stringify(xlamLinks));
  if (existsSync(join(root, "InstaWords.xlam"))) {
    const xlam = await page.evaluate(async () => {
      const r = await fetch("InstaWords.xlam");
      const b = new Uint8Array(await r.arrayBuffer());
      return { ok: r.ok, pk: b[0] === 0x50 && b[1] === 0x4b, size: b.length };
    });
    check("InstaWords.xlam downloadable (zip)", xlam.ok && xlam.pk && xlam.size > 10000, JSON.stringify(xlam));
  } else {
    console.log("  skip InstaWords.xlam not built");
  }
  check("install steps: Unblock, Add-ins dialog, Trusted Location", await page.evaluate(() => {
    const t = document.querySelector("#install").textContent;
    return ["إلغاء الحظر", "الوظائف الإضافية", "استعراض", "مركز التوثيق", "المواقع الموثوقة"].every((w) => t.includes(w));
  }));
  check("no download link sends the user to a GitHub page", await page.$$eval("a[download]", (as) => as.every((a) => !/github\.com/.test(a.href))));
  check("formula shown", (await page.textContent("#formula")).includes('=TAFQIT(A1, "ريال سعودي", "مائة", "فقط", "لا غير")'));

  /* ---------- offline via service worker ---------- */
  console.log("offline");
  await page.goto(BASE);
  await ready(page);
  const active = await page.evaluate(async () => !!(await navigator.serviceWorker.ready).active);
  check("service worker active", active);
  await page.waitForFunction(() => !!navigator.serviceWorker.controller, null, { timeout: 10000 }).catch(() => {});
  check("page controlled by service worker", await page.evaluate(() => !!navigator.serviceWorker.controller));
  await ctx.setOffline(true);
  await page.reload();
  await ready(page);
  await page.selectOption("#unit", "SAR");
  await page.fill("#number", "1250.75");
  await waitText(page, "#result", "ألف ومائتان وخمسون ريالاً سعودياً وخمس وسبعون هللة", "offline: single page converts");
  await page.goto(`${BASE}bulk.html`);
  await ready(page);
  await page.fill("#numbers", "1\n2\n3");
  await page.waitForFunction(() => document.querySelector("#rows")?.dataset.rendered === "3", null, { timeout: 5000 }).catch(() => {});
  check("offline: bulk page works", (await rowCount(page)) === 3);
  await page.goto(`${BASE}?n=7&unit=SAR`);
  await ready(page);
  await waitText(page, "#result", tafqit("7", { unit: "SAR" }), "offline: deep link");
  await page.goto(`${BASE}en/`);
  await ready(page);
  check("offline: English page served from the cache", (await page.getAttribute("html", "lang")) === "en");
  await page.selectOption("#unit", "SAR");
  await page.fill("#number", "1250.75");
  await waitText(page, "#result", tafqit("1250.75", { unit: "SAR", language: "en" }), "offline: English page converts (English by default)");
  await page.goto(`${BASE}en/bulk.html?unit=USD`);
  await ready(page);
  await page.fill("#numbers", "1\n2\nabc");
  await page.waitForFunction(() => document.querySelector("#rows")?.dataset.rendered === "3", null, { timeout: 5000 }).catch(() => {});
  check("offline: English bulk page works", (await rowCount(page)) === 3 && (await page.textContent("#rows tr:nth-child(3) td.c-text")) === "The number format is not valid.");
  await page.goto(`${BASE}en/excel.html`);
  await page.waitForLoadState("load");
  check("offline: English Excel page served", (await page.getAttribute("html", "lang")) === "en" && (await page.isVisible("#formula")));
  await page.goto(`${BASE}en/no-such-page.html`);
  await ready(page);
  check("offline: unknown English URL falls back to the English converter", (await page.getAttribute("html", "lang")) === "en");
  await page.goto(`${BASE}no-such-page.html`);
  await ready(page);
  check("offline: unknown Arabic URL falls back to the Arabic converter", (await page.getAttribute("html", "lang")) === "ar");
  // Offline with no cached release: real network (no mock), so the API call fails → fallback.
  await ctx.unroute("https://api.github.com/**");
  await page.evaluate(() => localStorage.removeItem("instawords.release.v1"));
  await page.goto(`${BASE}#download`);
  await page.waitForSelector("html[data-release]", { timeout: 15000 }).catch(() => {});
  check("offline: release falls back to the bundled installer", (await page.getAttribute("html", "data-release")) === "fallback");
  check("offline: #download dialog explains GitHub is unreachable", await page.isVisible("#download-dialog [data-release-offline]"));
  check("offline: dialog offers the static direct download", (await page.getAttribute("#download-dialog [data-dialog-download]", "href")) === STATIC_EXE);
  await ctx.setOffline(false);
  await ctx.close();

  /* ---------- layout: no horizontal scroll at 360px ---------- */
  console.log("layout");
  const narrow = await newContext({ viewport: { width: 360, height: 740 } });
  const np = watch(await narrow.newPage());
  for (const p of ["", "bulk.html", "excel.html", "en/", "en/bulk.html", "en/excel.html"]) {
    if (p.endsWith(".html") && !existsSync(join(root, p))) continue;
    await np.goto(BASE + p);
    await np.waitForLoadState("load");
    if (p.endsWith("bulk.html")) {
      await ready(np);
      await np.click("#sample");
      await np.waitForTimeout(150);
    }
    const [sw, cw] = await np.evaluate(() => [document.documentElement.scrollWidth, document.documentElement.clientWidth]);
    check(`no horizontal scroll at 360px: ${p || "index.html"}`, sw <= cw, `${sw} > ${cw}`);
    const header = await np.evaluate(() => {
      const s = document.querySelector("header [data-lang-switch]")?.getBoundingClientRect();
      return s && { left: s.left, right: s.right, top: s.top, w: document.documentElement.clientWidth, ltr: document.dir === "ltr" };
    });
    // On the first row, at the end side (right in English, left in Arabic), beside the brand.
    const atEnd = header && (header.ltr ? header.right >= header.w - 32 : header.left <= 32);
    check(`360px: header language switch on the first row, at the end: ${p || "index.html"}`, !!header && header.left >= 0 && header.right <= header.w && header.top < 60 && atEnd, JSON.stringify(header));
  }
  for (const p of ["", "en/"]) {
    await np.goto(`${BASE}${p}#download`);
    await np.waitForSelector("html[data-release]", { timeout: 10000 }).catch(() => {});
    await np.waitForSelector("#download-dialog[open] [data-release-notes-body]", { timeout: 5000 }).catch(() => {});
    const box = await np.evaluate(() => {
      const d = document.getElementById("download-dialog");
      const r = d.getBoundingClientRect();
      return { left: r.left, right: r.right, inner: d.querySelector(".dialog-body").scrollWidth <= d.querySelector(".dialog-body").clientWidth };
    });
    check(`#download dialog fits a 360px screen: ${p || "index.html"}`, box.left >= 0 && box.right <= 360 && box.inner, JSON.stringify(box));
  }
  await narrow.close();

  /* ---------- screenshots ---------- */
  const shots = process.env.TAFKET_SCREENSHOTS;
  if (shots) {
    mkdirSync(shots, { recursive: true });
    {
      const scheme = "light";
      for (const [label, viewport] of [["mobile", { width: 375, height: 812 }], ["desktop", { width: 1280, height: 900 }]]) {
        const c = await newContext({ viewport, colorScheme: scheme });
        const p = watch(await c.newPage());
        await p.goto(`${BASE}?n=1250.75&unit=SAR`);
        await ready(p);
        await p.evaluate(() => document.fonts.ready);
        await p.waitForTimeout(350);
        await p.screenshot({ path: join(shots, `index-${label}-${scheme}.png`), fullPage: true });
        await p.fill("#number", "abc");
        await p.waitForTimeout(350);
        await p.locator(".cheque").screenshot({ path: join(shots, `index-error-${label}-${scheme}.png`) });
        await p.goto(`${BASE}bulk.html`);
        await ready(p);
        await p.click("#sample");
        await p.waitForTimeout(200);
        await p.screenshot({ path: join(shots, `bulk-${label}-${scheme}.png`), fullPage: true });
        await p.goto(`${BASE}excel.html`);
        await p.waitForLoadState("load");
        await p.screenshot({ path: join(shots, `excel-${label}-${scheme}.png`), fullPage: true });
        await p.goto(`${BASE}#download`);
        await p.waitForSelector("#download-dialog[open] [data-release-notes-body]", { timeout: 5000 }).catch(() => {});
        await p.waitForTimeout(200);
        await p.screenshot({ path: join(shots, `download-dialog-${label}-${scheme}.png`) });
        await p.goto(`${BASE}#support`);
        await p.waitForTimeout(200);
        await p.screenshot({ path: join(shots, `support-dialog-${label}-${scheme}.png`) });
        await c.close();
      }
    }
    await englishScreenshots(join(shots, "en"));
    console.log(`screenshots → ${shots}`);
  }

  // Light theme only: a dark-mode preference must not change the page.
  const dark = await newContext({ colorScheme: "dark" });
  const dp = watch(await dark.newPage());
  await dp.goto(BASE);
  await ready(dp);
  const bg = await dp.evaluate(() => getComputedStyle(document.body).backgroundColor);
  check("light theme even when the system prefers dark", bg === "rgb(255, 255, 255)", bg);
  await dark.close();

  await githubLinkChecks();
  await apiDownChecks();

  check("no console errors", consoleErrors.length === 0, consoleErrors.join(" | "));
  check("no requests outside the site (fully offline-capable)", externalRequests.length === 0, [...new Set(externalRequests)].join(" | "));
} catch (err) {
  failures.push(`unexpected: ${err.stack ?? err}`);
  console.error(err);
} finally {
  await browser.close();
  server.close();
}

async function languageChecks() {
  const langs = REAL_LANGUAGES ?? STUB_LANGUAGES;
  const c = await newContext({ serviceWorkers: "block", permissions: ["clipboard-read", "clipboard-write"] });
  if (!REAL_LANGUAGES) {
    await c.route("**/lib/index.js", async (route) => {
      const res = await route.fetch();
      const body = (await res.text()) + `\nexport const LANGUAGES = ${JSON.stringify(STUB_LANGUAGES)};\n`;
      await route.fulfill({ response: res, body });
    });
  }
  const p = watch(await c.newPage());
  const en = langs.find((l) => l.id === "en");
  const fr = langs.find((l) => l.id === "fr");
  try {
    await p.goto(BASE);
    await ready(p);
    const ids = await p.$$eval('input[name="language"]', (rs) => rs.map((r) => r.value));
    check("picker lists the core languages", ids.join() === langs.map((l) => l.id).join() && (await p.isVisible("[data-language-field]")), ids.join());
    check("Arabic selected by default", await p.isChecked('input[name="language"][value="ar"]'));
    if (!en) return;
    await p.selectOption("#unit", "SAR");
    await p.click('[data-preset="only"]');
    await p.check('input[name="language"][value="en"]');
    check("hundreds control hidden for English", await p.isHidden("[data-hundreds-field]"));
    check('preset switches to "… Only"', (await p.inputValue("#prefix")) === "" && (await p.inputValue("#suffix")) === "Only", `${await p.inputValue("#prefix")}|${await p.inputValue("#suffix")}`);
    await p.click('[data-preset="none"]');
    await p.fill("#number", "1250.75");
    await p.waitForTimeout(50);
    const r = await p.evaluate(() => ({ dir: document.getElementById("result").dir, lang: document.getElementById("result").lang, cls: document.getElementById("result").className }));
    if (REAL_LANGUAGES) {
      const want = core.tryTafqit("1250.75", { unit: "SAR", language: "en" });
      await waitText(p, "#result", want.ok ? want.result : want.error.message, `English with SAR matches the core (${want.ok ? "text" : want.error.code})`);
      const u = unitFor("en");
      await p.selectOption("#unit", u);
      await p.fill("#number", "2,500.5");
      await waitText(p, "#result", tafqit("2,500.5", { unit: u, language: "en" }), `English output matches the core (unit ${u})`);
      const r2 = await p.evaluate(() => ({ dir: document.getElementById("result").dir, lang: document.getElementById("result").lang }));
      check("English result is LTR", r2.dir === "ltr" && r2.lang === "en", JSON.stringify(r2));
    } else {
      check("English result marked LTR (stub)", r.dir === "ltr" && r.lang === "en", JSON.stringify(r));
    }
    await p.reload();
    await ready(p);
    check("language remembered after reload", await p.isChecked('input[name="language"][value="en"]'));
    await p.check('input[name="language"][value="ar"]');
    check("hundreds control back for Arabic", await p.isVisible("[data-hundreds-field]"));
    await p.goto(`${BASE}?lang=en&n=7&unit=SAR`);
    await ready(p);
    check("deep link lang=en selects English", await p.isChecked('input[name="language"][value="en"]'));
    if (fr) {
      await p.goto(`${BASE}?lang=fr&n=1250.75&unit=SAR`);
      await ready(p);
      check("deep link lang=fr selects French", await p.isChecked('input[name="language"][value="fr"]'));
      await p.click('[data-preset="only"]');
      check("French preset is a prefix", (await p.inputValue("#prefix")).startsWith("Arrêtée") && (await p.inputValue("#suffix")) === "");
      if (REAL_LANGUAGES) {
        const u = unitFor("fr");
        await p.selectOption("#unit", u);
        const want = core.tryTafqit("1250.75", { unit: u, language: "fr", prefix: await p.inputValue("#prefix") });
        await waitText(p, "#result", want.ok ? want.result : want.error.message, `French output matches the core (unit ${u})`);
      }
      await p.click('[data-preset="none"]');
    }
    await p.goto(`${BASE}bulk.html?lang=en&unit=${unitFor("en")}`);
    await ready(p);
    check("bulk: language picker and deep link", await p.isChecked('input[name="language"][value="en"]'));
    await p.fill("#numbers", "1\n2500");
    await p.waitForFunction(() => document.querySelector("#rows")?.dataset.rendered === "2", null, { timeout: 5000 }).catch(() => {});
    const cell = await p.evaluate(() => { const td = document.querySelector("#rows tr:not(.is-error) td.c-text"); return td && { dir: td.dir, lang: td.lang, text: td.textContent }; });
    if (REAL_LANGUAGES) {
      check("bulk: English rows", cell?.text === tafqit("1", { unit: await p.inputValue("#unit"), language: "en" }) && cell?.dir === "ltr", JSON.stringify(cell));
    } else {
      check("bulk: English rows marked LTR (stub)", cell?.dir === "ltr" && cell?.lang === "en", JSON.stringify(cell));
    }
    const shots = process.env.TAFKET_SCREENSHOTS;
    if (shots) {
      mkdirSync(shots, { recursive: true });
      const tag = REAL_LANGUAGES ? "" : "-stub";
      for (const [label, viewport] of [["mobile", { width: 375, height: 812 }], ["desktop", { width: 1280, height: 900 }]]) {
        await p.setViewportSize(viewport);
        for (const l of langs.filter((x) => x.id !== "ar")) {
          await p.goto(`${BASE}?lang=${l.id}&n=1250.75&unit=${unitFor(l.id)}`);
          await ready(p);
          await p.evaluate(() => document.fonts.ready);
          await p.waitForTimeout(350);
          await p.screenshot({ path: join(shots, `index-${l.id}${tag}-${label}.png`), fullPage: label === "mobile" ? false : false });
        }
        await p.goto(`${BASE}bulk.html?lang=en&unit=${unitFor("en")}`);
        await ready(p);
        await p.click("#sample");
        await p.waitForTimeout(200);
        await p.screenshot({ path: join(shots, `bulk-en${tag}-${label}.png`), fullPage: true });
      }
    }
  } finally {
    await c.close();
  }
}

/** The on-site release dialog (#download) and the support dialog (#support), with the API fixture. */
async function releaseDialogChecks(page) {
  console.log("release & support dialogs");
  const trigger = "#windows [data-release-notes]";
  await page.click(trigger);
  check("release-notes link opens the #download dialog", await page.isVisible("#download-dialog[open]"));
  check("dialog is modal, labelled and RTL", await page.evaluate(() => {
    const d = document.getElementById("download-dialog");
    return d.matches(":modal") && d.dir === "rtl" && document.getElementById(d.getAttribute("aria-labelledby"))?.textContent.trim().length > 0;
  }));
  check("opening the dialog sets #download", (await page.evaluate(() => location.hash)) === "#download");
  const d = await page.evaluate(() => {
    const q = (s) => document.querySelector(`#download-dialog ${s}`);
    return {
      version: q("[data-release-version]")?.textContent,
      text: q(".dialog-body")?.textContent ?? "",
      sha: q("[data-sha256]")?.textContent,
      primary: q("[data-dialog-download]")?.href,
      primaryText: q("[data-dialog-download]")?.textContent ?? "",
      portable: q("[data-portable-download]")?.href,
      github: [...document.querySelectorAll("#download-dialog a[href*='github.com']")].filter((a) => !a.href.includes("/releases/download/")).map((a) => ({ gh: a.hasAttribute("data-github"), text: a.textContent })),
    };
  });
  check("dialog: version, date and installer size", d.version === `v${FV}` && d.text.includes("2026") && d.text.includes("2.5 ميغابايت"), d.text.slice(0, 200));
  check("dialog: SHA-256 from the asset digest", d.sha === FIXTURE_SHA, String(d.sha));
  check("dialog: requirements (Windows 10/11, no admin, offline)", /ويندوز 10 أو 11/.test(d.text) && d.text.includes("صلاحيات المسؤول") && d.text.includes("دون إنترنت"));
  check("dialog: primary button downloads the installer directly", d.primary === FIXTURE_EXE && d.primaryText.includes(`v${FV}`), String(d.primary));
  check("dialog: portable zip is a direct link", d.portable === FIXTURE_ZIP, String(d.portable));
  check("dialog: one explicit GitHub link", d.github.length === 1 && d.github[0].gh && d.github[0].text.includes("GitHub"), JSON.stringify(d.github));
  const notes = await page.evaluate(() => {
    const b = document.querySelector("#download-dialog [data-release-notes-body]");
    return b && {
      h: [...b.querySelectorAll("h3, h4")].map((e) => e.textContent),
      li: b.querySelectorAll("li").length,
      ol: b.querySelectorAll("ol > li").length,
      strong: b.querySelectorAll("strong").length,
      code: b.querySelector("code")?.textContent,
      elements: [...b.querySelectorAll("*")].map((e) => e.tagName.toLowerCase()),
      text: b.textContent,
      pwned: window.__pwned,
    };
  });
  check("release notes: headings, lists, bold and code rendered", !!notes && notes.h.includes(`InstaWords — تفقيط v${FV}`) && notes.h.includes("Fixes") && notes.li === 4 && notes.ol === 2 && notes.strong >= 2 && notes.code === `InstaWords_${FV}_x64-setup.exe`, JSON.stringify(notes));
  check("release notes: remote HTML stays text (no <script>/<img>, nothing executed)", !!notes && notes.elements.every((t) => ["h3", "h4", "p", "ul", "ol", "li", "strong", "code"].includes(t)) && notes.text.includes("<img src=x") && notes.text.includes("<script>") && notes.pwned === undefined, JSON.stringify(notes && { elements: [...new Set(notes.elements)], pwned: notes.pwned }));
  check("release notes: GitHub layout tags dropped", !!notes && !notes.text.includes("<div") && !notes.text.includes("</div>"));
  await page.keyboard.press("Escape");
  await closed(page, "#download-dialog");
  check("Esc closes the dialog", !(await page.isVisible("#download-dialog")) && (await page.evaluate(() => location.hash)) === "");
  check("focus returns to the link that opened it", await page.evaluate((s) => document.activeElement === document.querySelector(s), trigger));

  await page.goto(`${BASE}#download`);
  await ready(page);
  await page.waitForSelector("#download-dialog[open]", { timeout: 5000 }).catch(() => {});
  check("#download deep link opens the dialog", await page.isVisible("#download-dialog[open]"));
  check("deep-linked dialog shows the latest release", (await page.textContent("#download-dialog [data-release-version]").catch(() => "")) === `v${FV}`);
  await page.click("#download-dialog .dialog-close");
  await closed(page, "#download-dialog");
  check("close button closes the dialog", !(await page.isVisible("#download-dialog")));

  await page.click("footer [data-support]");
  check("footer report link opens the on-site support dialog", await page.isVisible("#support-dialog[open]"));
  const sup = await page.evaluate(() => {
    const d = document.getElementById("support-dialog");
    return { text: d.textContent, links: [...d.querySelectorAll("a")].map((a) => ({ href: a.href, gh: a.hasAttribute("data-github"), text: a.textContent })) };
  });
  check("support dialog explains the app, Excel and website options", sup.text.includes("برنامج ويندوز") && sup.text.includes("Excel") && sup.text.includes("الموقع"));
  check("support dialog: exactly one explicit GitHub button to open an issue", sup.links.length === 1 && sup.links[0].href === `${REPO_URL}/issues/new/choose` && sup.links[0].gh && sup.links[0].text.includes("GitHub"), JSON.stringify(sup.links));
  await page.keyboard.press("Escape");
  await closed(page, "#support-dialog");
  check("Esc closes the support dialog, focus back on its link", !(await page.isVisible("#support-dialog")) && (await page.evaluate(() => document.activeElement?.matches("footer [data-support]"))));
  await page.goto(BASE);
  await ready(page);
}

/** Leaving the site: only explicit GitHub buttons/links, plus direct release files, may point at github.com. */
async function githubLinkChecks() {
  console.log("links out to GitHub");
  for (const name of PAGES) {
    if (!existsSync(join(root, name))) continue;
    const html = readFileSync(join(root, name), "utf8");
    const staticHrefs = [...html.matchAll(/<a\b[^>]*\bdata-windows-download\b[^>]*>/g)].map((m) => /href="([^"]+)"/.exec(m[0])?.[1]);
    check(`${name}: static (no-JS) Windows links download the installer directly`, staticHrefs.length >= 1 && staticHrefs.every((h) => h === STATIC_EXE), staticHrefs.join(", "));
    const labels = [...html.matchAll(/<span\b[^>]*\bdata-version\b[^>]*>([^<]*)<\/span>/g)].map((m) => m[1]);
    check(`${name}: static version labels show the fallback ${FB_V} (site/release-fallback.json)`, labels.length >= 1 && labels.every((l) => l === FB_V), labels.join(", "));
    const files = [...html.matchAll(/https:\/\/github\.com\/[^"'\s<>]*\/releases\/download\/[^"'\s<>]+/g)].map((m) => m[0]);
    check(`${name}: every release file named in the HTML is the fallback release`, files.every((f) => f === STATIC_EXE || f === FB.portable?.url), files.join(", "));
  }
  for (const name of ["index.html", "en/index.html"]) {
    const ld = /<script type="application\/ld\+json">([\s\S]*?)<\/script>/.exec(readFileSync(join(root, name), "utf8"));
    const app = ld && JSON.parse(ld[1])["@graph"]?.find?.((n) => n["@type"] === "WebApplication");
    check(`${name}: JSON-LD downloadUrl is the fallback installer`, app?.downloadUrl === STATIC_EXE, String(app?.downloadUrl));
  }
  const releaseJs = readFileSync(join(root, "assets", "release.js"), "utf8");
  check("assets/release.js: FALLBACK_DATA filled from site/release-fallback.json", releaseJs.includes(`/* @release-fallback */ ${JSON.stringify(FB)}`) && !releaseJs.includes("/* @release-fallback */ null"));
  const c = await newContext({ serviceWorkers: "block" });
  const p = watch(await c.newPage());
  try {
    for (const path of ["", "bulk.html", "excel.html", "en/", "en/bulk.html", "en/excel.html"]) {
      if (path.endsWith(".html") && !existsSync(join(root, path))) continue;
      await p.goto(BASE + path);
      await p.waitForSelector("html[data-release]", { timeout: 10000 }).catch(() => {});
      for (const hash of ["#download", "#support"]) {
        await p.evaluate((h) => { location.hash = h; }, hash);
        await p.waitForSelector(`${hash}-dialog[open]`, { timeout: 5000 }).catch(() => {});
        await p.keyboard.press("Escape");
        await closed(p, `${hash}-dialog`);
      }
      const links = await p.$$eval("a[href]", (as) => as.map((a) => ({ href: a.href, gh: a.hasAttribute("data-github"), text: a.textContent.trim(), dl: a.hasAttribute("data-windows-download") || a.hasAttribute("data-portable-download") })));
      const out = links.filter((l) => /^https?:\/\/([^/]+\.)?github\.com\//.test(l.href));
      const files = out.filter((l) => /\/releases\/download\/v[\d.]+\/InstaWords_[\d.]+_x64-(setup\.exe|portable\.zip)$/.test(l.href));
      const bad = out.filter((l) => !files.includes(l) && !(l.gh && l.text.includes("GitHub")));
      check(`${path || "index.html"}: every github.com link is an explicit GitHub button (${out.length - files.length}) or a direct file`, bad.length === 0 && files.every((l) => l.dl), bad.map((l) => `${l.text} → ${l.href}`).join(" | "));
      const pages = out.filter((l) => /\/releases(\/latest|\/tag\/|\/?$)/.test(l.href) && !l.gh);
      check(`${path || "index.html"}: no download link to the GitHub releases page`, pages.length === 0, pages.map((l) => l.href).join(", "));
      check(`${path || "index.html"}: footer report link stays on the site`, (await p.getAttribute("footer [data-support]", "href")) === "#support");
      // Every other link stays on the site (FastKiwi included) and points at a page or file that exists.
      const site = new URL(BASE);
      const offSite = links.filter((l) => !out.includes(l) && !/^https:\/\/fastkiwi\.app\//.test(l.href) && new URL(l.href).origin !== site.origin);
      check(`${path || "index.html"}: no other link leaves the site`, offSite.length === 0, offSite.map((l) => `${l.text} → ${l.href}`).join(" | "));
      const missing = [];
      for (const l of links) {
        const u = new URL(l.href);
        if (u.origin !== site.origin || !u.pathname.startsWith(PREFIX)) continue;
        let file = join(root, decodeURIComponent(u.pathname.slice(PREFIX.length)));
        if (u.pathname.endsWith("/")) file = join(file, "index.html");
        if (!existsSync(file)) missing.push(`${l.text} → ${u.pathname}`);
      }
      check(`${path || "index.html"}: every on-site link resolves to a built file`, missing.length === 0, missing.join(" | "));
    }
  } finally {
    await c.close();
  }
}

/** GitHub unreachable: buttons keep the bundled direct download, the dialog says so. */
async function apiDownChecks() {
  console.log("GitHub API unreachable");
  const c = await newContext({ serviceWorkers: "block" }, "down");
  const p = await c.newPage();
  const errors = [];
  p.on("pageerror", (e) => errors.push(e.message));
  try {
    await p.goto(`${BASE}#download`);
    await ready(p);
    await p.waitForSelector("html[data-release]", { timeout: 15000 }).catch(() => {});
    check("API down: release falls back", (await p.getAttribute("html", "data-release")) === "fallback");
    const links = await p.$$eval("[data-windows-download]", (as) => as.map((a) => ({ href: a.href, text: a.textContent })));
    check(`API down: buttons download the bundled installer directly and show ${FB_V}`, links.length >= 4 && links.every((l) => l.href === STATIC_EXE && l.text.includes(FB_V)), links.map((l) => l.href).join(", "));
    check("API down: dialog open and says GitHub is unreachable", (await p.isVisible("#download-dialog[open]")) && (await p.isVisible("#download-dialog [data-release-offline]")));
    const d = await p.evaluate(() => ({
      primary: document.querySelector("#download-dialog [data-dialog-download]")?.href,
      portable: document.querySelector("#download-dialog [data-portable-download]")?.href,
      notes: !!document.querySelector("#download-dialog [data-release-notes-body]"),
      github: [...document.querySelectorAll("#download-dialog a[href*='github.com']")].filter((a) => !a.href.includes("/releases/download/")).map((a) => ({ gh: a.hasAttribute("data-github"), text: a.textContent })),
    }));
    check(`API down: dialog offers the ${FB_V} direct download (+ portable)`, d.primary === STATIC_EXE && d.portable === FB.portable?.url && !d.notes, JSON.stringify(d));
    check("API down: exactly one clearly labelled GitHub link", d.github.length === 1 && d.github[0].gh && d.github[0].text.includes("GitHub"), JSON.stringify(d.github));
    check("API down: no page errors", errors.length === 0, errors.join(" | "));
  } finally {
    await c.close();
  }
}

/** Where Arabic letters appear outside an element marked lang="ar" (UI chrome that was not translated). */
function arabicChrome(page) {
  return page.evaluate((src) => {
    const AR = new RegExp(src);
    const bad = [];
    const outsideAr = (el) => el.closest("[lang]")?.getAttribute("lang") !== "ar" && !el.closest("[data-release-notes-body], script, style");
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    for (let n; (n = walker.nextNode()); ) {
      if (n.parentElement && AR.test(n.data) && outsideAr(n.parentElement)) bad.push(n.data.trim().slice(0, 50));
    }
    for (const el of document.body.querySelectorAll("[aria-label], [title], [placeholder], [alt], optgroup")) {
      for (const a of ["aria-label", "title", "placeholder", "alt", "label"]) {
        const v = el.getAttribute(a);
        if (v && AR.test(v) && outsideAr(el)) bad.push(`${a}="${v}"`);
      }
    }
    for (const m of document.head.querySelectorAll('title, meta[name="description"], meta[property^="og:"], meta[name^="twitter:"]')) {
      const v = m.tagName === "TITLE" ? m.textContent : m.getAttribute("content");
      if (AR.test(v)) bad.push(`head: ${v.slice(0, 50)}`);
    }
    return bad;
  }, ARABIC_LETTERS.source);
}

/** Head of every page: lang/dir, canonical and the ar ↔ en ↔ x-default alternates (static HTML). */
function headChecks() {
  console.log("page heads: lang/dir, canonical, hreflang");
  for (const name of PAGES) {
    if (!existsSync(join(root, name))) continue;
    const html = readFileSync(join(root, name), "utf8");
    const en = name.startsWith("en/");
    const page = name.replace(/^en\//, "").replace(/^index\.html$/, "");
    const tag = /<html\b[^>]*>/.exec(html)?.[0] ?? "";
    check(`${name}: <html lang dir>`, tag.includes(en ? 'lang="en"' : 'lang="ar"') && tag.includes(en ? 'dir="ltr"' : 'dir="rtl"'), tag);
    const canonical = /<link rel="canonical" href="([^"]+)">/.exec(html)?.[1];
    check(`${name}: canonical`, canonical === `${SITE_URL}${en ? "en/" : ""}${page}`, String(canonical));
    const alt = Object.fromEntries([...html.matchAll(/<link rel="alternate" hreflang="([^"]+)" href="([^"]+)">/g)].map((m) => [m[1], m[2]]));
    const want = { ar: `${SITE_URL}${page}`, en: `${SITE_URL}en/${page}`, "x-default": `${SITE_URL}en/${page}` };
    check(`${name}: hreflang ar / en / x-default=en`, JSON.stringify(alt) === JSON.stringify(want), JSON.stringify(alt));
    const ogUrl = /<meta property="og:url" content="([^"]+)">/.exec(html)?.[1];
    check(`${name}: og:url is the canonical`, ogUrl === canonical, String(ogUrl));
    const locale = /<meta property="og:locale" content="([^"]+)">/.exec(html)?.[1];
    check(`${name}: og:locale`, locale === (en ? "en_US" : "ar_AR"), String(locale));
    const sw = [...html.matchAll(/<a\b[^>]*\bdata-lang-switch\b[^>]*>([^<]*)<\/a>/g)].map((m) => ({ tag: m[0], text: m[1] }));
    const target = en ? `../${page}` : `en/${page}`;
    check(`${name}: language switch in header and footer → ${target}`, sw.length === 2 && sw.every((l) => l.tag.includes(`href="${target}"`) && l.text === (en ? "العربية" : "English") && l.tag.includes(`hreflang="${en ? "ar" : "en"}"`)), JSON.stringify(sw));
    if (en) {
      const desc = /<meta name="description" content="([^"]+)">/.exec(html)?.[1] ?? "";
      const tw = /<meta name="twitter:title" content="([^"]+)">/.exec(html)?.[1] ?? "";
      check(`${name}: English description, og and twitter tags`, desc.length > 80 && !ARABIC_LETTERS.test(desc) && tw.length > 10 && html.includes('content="https://fastkiwi.app/img/social-en.png"'), desc);
    }
  }
  const ld = /<script type="application\/ld\+json">([\s\S]*?)<\/script>/.exec(readFileSync(join(root, "en/index.html"), "utf8"));
  const app = ld && JSON.parse(ld[1])["@graph"]?.find?.((n) => n["@type"] === "WebApplication");
  check("en/index.html: JSON-LD WebApplication in English (inLanguage en, en/ URL, FastKiwi)", app?.inLanguage === "en" && app?.url === `${SITE_URL}en/` && app?.publisher?.["@id"] === "https://fastkiwi.app/#org" && !ARABIC_LETTERS.test(app?.description ?? "ا"), JSON.stringify(app && { inLanguage: app.inLanguage, url: app.url }));
  const sw = readFileSync(join(root, "sw.js"), "utf8");
  check("sw.js precaches both language trees", ["\"./\"", "\"en/\"", "\"en/index.html\"", "\"en/bulk.html\"", "\"index.html\"", "\"assets/i18n.js\""].every((a) => sw.includes(a)));
}

/** The English interface: /instawords/en/ (strings, default output language, conversion, dialogs). */
async function englishChecks() {
  headChecks();
  console.log("English single page");
  const EN = `${BASE}en/`;
  const c = await newContext({ permissions: ["clipboard-read", "clipboard-write"], acceptDownloads: true });
  const p = watch(await c.newPage());
  try {
    await p.goto(EN);
    await ready(p);
    check("en: lang/dir", (await p.getAttribute("html", "lang")) === "en" && (await p.getAttribute("html", "dir")) === "ltr");
    check("en: title in English", /^InstaWords — Amounts in words/.test(await p.title()), await p.title());
    check("en: header in English", (await p.textContent(".tool-nav")).replace(/\s+/g, " ").trim() === "Convert Bulk Excel" && (await p.getAttribute(".brand", "href")) === "https://fastkiwi.app/");
    const sw = await p.$$eval("a[data-lang-switch]", (as) => as.map((a) => ({ href: a.href, text: a.textContent.trim(), lang: a.lang })));
    check("en: «العربية» switch in header and footer → the Arabic page", sw.length === 2 && sw.every((l) => l.href === BASE && l.text === "العربية" && l.lang === "ar"), JSON.stringify(sw));
    if (REAL_LANGUAGES) {
      const ids = await p.$$eval('input[name="language"]', (rs) => rs.map((r) => r.value));
      check("en: output language picker (English first, Arabic and French offered)", ids[0] === "en" && ids.includes("ar") && ids.includes("fr") && (await p.isVisible("[data-language-field]")), ids.join());
      check("en: English output selected by default", await p.isChecked('input[name="language"][value="en"]'));
      check("en: hundreds control hidden for English output", await p.isHidden("[data-hundreds-field]"));
    }
    const groups = await p.$$eval("#unit optgroup", (gs) => gs.map((g) => g.label));
    check("en: unit groups in English", groups[0] === "No unit" && groups.includes("Arab currencies") && groups.includes("World currencies") && groups.length >= 13, groups.join(", "));
    const opts = await p.$$eval("#unit option", (os) => Object.fromEntries(os.map((o) => [o.value, o.textContent])));
    check("en: unit names in English", opts.SAR === "Saudi Riyal (SAR)" && opts.none === "No unit" && opts.USD === "US Dollar (USD)", `${opts.SAR} | ${opts.none} | ${opts.USD}`);
    check("en: empty state in English", (await p.textContent("#result")) === "Type an amount in the field above to see it here in words." && (await p.getAttribute("#result", "dir")) === "ltr");
    const chrome = await arabicChrome(p);
    check("en: no Arabic UI text outside lang=ar (switch, picker, Arabic samples)", chrome.length === 0, chrome.join(" | "));
    const examples = await p.$$eval("[data-example]", (els) => els.map((e) => ({ n: e.dataset.n, unit: e.dataset.unit, language: e.dataset.lang, text: e.textContent.trim(), lang: e.closest("[lang]").lang })));
    const wrongEx = examples.filter((e) => e.text !== tafqit(e.n, { unit: e.unit, language: e.language }) || e.lang !== e.language);
    check(`en: worked examples match the engine (${examples.length}, English first)`, examples.length >= 6 && examples[0].language === "en" && wrongEx.length === 0, wrongEx.map((e) => `${e.n} ${e.unit} ${e.language}: «${e.text}»`).join("; "));
    const faqs = await p.$$eval(".faq details summary", (els) => els.map((e) => e.textContent));
    check("en: FAQ in English", faqs.length >= 7 && faqs.every((f) => !ARABIC_LETTERS.test(f)), faqs.join(" | "));
    check("en: footer in English", /free and open source/.test(await p.textContent("footer")) && (await p.getAttribute('footer a[href$="privacy/"]', "href")) === "https://fastkiwi.app/privacy/");

    if (REAL_LANGUAGES) {
      await p.selectOption("#unit", "SAR");
      await p.fill("#number", "1250.75");
      await waitText(p, "#result", "One Thousand Two Hundred Fifty Saudi Riyals and Seventy-Five Halalas", "en: 1250.75 SAR in English by default");
      check("en: English result is LTR", (await p.getAttribute("#result", "dir")) === "ltr" && (await p.getAttribute("#result", "lang")) === "en");
      await waitText(p, "#cleaned", "1,250.75", "en: number used");
      await p.fill("#number", "١٢٣");
      await waitText(p, "#result", tafqit("123", { unit: "SAR", language: "en" }), "en: Arabic-Indic digits");
      await p.fill("#number", "abc");
      await waitText(p, "#result", "The number format is not valid.", "en: errors in English (INVALID_FORMAT)");
      check("en: error is LTR, copy disabled", (await p.getAttribute("#result", "dir")) === "ltr" && (await p.isDisabled("#copy")));
      await p.fill("#number", "-5");
      await waitText(p, "#result", "Negative numbers are not supported.", "en: NEGATIVE in English");
      await p.fill("#number", "10.125");
      await p.check('input[name="decimals"][value="error"]');
      await waitText(p, "#result", "There are more decimal places than this unit allows.", "en: TOO_MANY_DECIMALS in English");
      await p.check('input[name="decimals"][value="round"]');
      await waitText(p, "#result", "Ten Saudi Riyals and Thirteen Halalas", "en: rounding by default");
      await p.selectOption("#unit", "piece");
      await p.fill("#number", "2.5");
      await waitText(p, "#result", "This unit does not support fractions.", "en: FRACTION_NOT_SUPPORTED in English");
      await p.selectOption("#unit", "SAR");
      await p.fill("#number", "2500");
      await p.click('[data-preset="only"]');
      check('en: "… Only" preset', (await p.inputValue("#suffix")) === "Only" && (await p.inputValue("#prefix")) === "");
      await waitText(p, "#result", "Two Thousand Five Hundred Saudi Riyals Only", "en: preset applied");
      await p.click("#copy");
      await p.waitForTimeout(100);
      check("en: copy to clipboard", (await p.evaluate(() => navigator.clipboard.readText())) === "Two Thousand Five Hundred Saudi Riyals Only");
      check("en: copy feedback in English", (await p.textContent("#copy")).includes("Copied") && (await p.textContent("#copy-status")) === "Copied to the clipboard.");

      // Arabic and French output on the English page.
      await p.check('input[name="language"][value="ar"]');
      check("en: hundreds control shown for Arabic output", await p.isVisible("[data-hundreds-field]"));
      check("en: preset swapped to فقط … لا غير", (await p.inputValue("#prefix")) === "فقط" && (await p.inputValue("#suffix")) === "لا غير");
      await waitText(p, "#result", "فقط ألفان وخمسمائة ريال سعودي لا غير", "en: Arabic output when chosen");
      check("en: Arabic result is RTL", (await p.getAttribute("#result", "dir")) === "rtl" && (await p.getAttribute("#result", "lang")) === "ar");
      await p.check('input[name="hundreds"][value="مئة"]');
      await p.fill("#number", "300");
      await waitText(p, "#result", "فقط ثلاثمئة ريال سعودي لا غير", "en: hundreds form for Arabic output");
      const chromeAr = await arabicChrome(p);
      check("en: with Arabic output, Arabic text only in lang=ar elements", chromeAr.length === 0, chromeAr.join(" | "));
      await p.fill("#number", "abc");
      await waitText(p, "#result", "The number format is not valid.", "en: errors stay English with Arabic output");
      await p.check('input[name="language"][value="fr"]');
      await p.click('[data-preset="none"]');
      await p.fill("#number", "1250.75");
      await waitText(p, "#result", tafqit("1250.75", { unit: "SAR", language: "fr" }), "en: French output");
      await p.check('input[name="language"][value="en"]');

      // Preferences: remembered per interface language.
      await p.selectOption("#unit", "KWD");
      await p.reload();
      await ready(p);
      check("en: prefs remembered", (await p.inputValue("#unit")) === "KWD" && (await p.isChecked('input[name="language"][value="en"]')));
      await p.goto(BASE);
      await ready(p);
      check("Arabic page keeps its own output language (Arabic)", await p.isChecked('input[name="language"][value="ar"]'));
      await p.goto(`${EN}?n=5000&unit=USD`);
      await ready(p);
      await waitText(p, "#result", "Five Thousand US Dollars", "en: deep link ?n=&unit=");
      check("en: language switch keeps the deep link", (await p.getAttribute("header [data-lang-switch]", "href")).endsWith("../?n=5000&unit=USD") || (await p.$eval("header [data-lang-switch]", (a) => a.href)) === `${BASE}?n=5000&unit=USD`);
      await p.goto(`${EN}?n=7&unit=SAR&lang=ar`);
      await ready(p);
      await waitText(p, "#result", tafqit("7", { unit: "SAR" }), "en: deep link lang=ar");
      await p.check('input[name="language"][value="en"]');
    }
    const arSw = await (async () => {
      await p.goto(BASE);
      await ready(p);
      return p.$$eval("a[data-lang-switch]", (as) => as.map((a) => ({ href: a.href, text: a.textContent.trim(), lang: a.lang })));
    })();
    check("ar: «English» switch in header and footer → the English page", arSw.length === 2 && arSw.every((l) => l.href === EN && l.text === "English" && l.lang === "en"), JSON.stringify(arSw));

    // Dialogs in English.
    console.log("English dialogs");
    await p.goto(EN);
    await ready(p);
    await p.waitForSelector("html[data-release]", { timeout: 10000 }).catch(() => {});
    const win = await p.$$eval("[data-windows-download]", (as) => as.map((a) => ({ href: a.href, text: a.textContent })));
    check("en: Windows download links → latest installer", win.length >= 4 && win.every((l) => l.href === FIXTURE_EXE && l.text.includes(`v${FV}`)), win.map((l) => l.href).join(", "));
    const trigger = "#windows [data-release-notes]";
    await p.click(trigger);
    await p.waitForSelector("#download-dialog[open]", { timeout: 5000 }).catch(() => {});
    const d = await p.evaluate(() => {
      const dlg = document.getElementById("download-dialog");
      const q = (s) => dlg.querySelector(s);
      return {
        dir: dlg.dir,
        lang: dlg.lang,
        modal: dlg.matches(":modal"),
        title: document.getElementById(dlg.getAttribute("aria-labelledby"))?.textContent,
        close: q(".dialog-close")?.getAttribute("aria-label"),
        text: q(".dialog-body")?.textContent ?? "",
        primary: q("[data-dialog-download]")?.textContent ?? "",
        portable: q("[data-portable-download]")?.href,
        github: [...dlg.querySelectorAll("a[href*='github.com']")].filter((a) => !a.href.includes("/releases/download/")).map((a) => ({ gh: a.hasAttribute("data-github"), text: a.textContent })),
      };
    });
    check("en: #download dialog is modal, LTR and English", d.modal && d.dir === "ltr" && d.lang === "en" && d.title === "InstaWords for Windows: download and what's new" && d.close === "Close", JSON.stringify({ dir: d.dir, lang: d.lang, title: d.title }));
    check("en: dialog facts in English (version, date, 2.5 MB)", d.text.includes("Version") && d.text.includes("December 1, 2026") && d.text.includes("2.5 MB") && d.text.includes("Installer SHA-256"), d.text.slice(0, 200));
    check("en: dialog requirements in English", d.text.includes("Windows 10 or 11 (64-bit)") && d.text.includes("No admin rights") && d.text.includes("Works offline"));
    check("en: dialog buttons in English", d.primary.includes("Download the installer") && d.primary.includes(`v${FV}`) && d.portable === FIXTURE_ZIP);
    check("en: dialog has one explicit GitHub link", d.github.length === 1 && d.github[0].gh && d.github[0].text === "All releases on GitHub", JSON.stringify(d.github));
    const dChrome = await arabicChrome(p);
    check("en: #download dialog has no Arabic UI text (remote notes aside)", dChrome.length === 0, dChrome.join(" | "));
    await p.keyboard.press("Escape");
    await closed(p, "#download-dialog");
    check("en: Esc closes the dialog, focus back on its link", !(await p.isVisible("#download-dialog")) && (await p.evaluate((s) => document.activeElement === document.querySelector(s), trigger)));
    await p.click("footer [data-support]");
    const sup = await p.evaluate(() => {
      const dlg = document.getElementById("support-dialog");
      return { open: dlg.open, dir: dlg.dir, text: dlg.textContent, links: [...dlg.querySelectorAll("a")].map((a) => ({ href: a.href, gh: a.hasAttribute("data-github"), text: a.textContent })) };
    });
    check("en: support dialog in English (Windows app, Excel, website)", sup.open && sup.dir === "ltr" && sup.text.includes("Windows app:") && sup.text.includes("Excel:") && sup.text.includes("Website:") && sup.text.includes('"en")'), sup.text.slice(0, 120));
    check("en: support dialog has one explicit GitHub button", sup.links.length === 1 && sup.links[0].href === `${REPO_URL}/issues/new/choose` && sup.links[0].gh && sup.links[0].text === "Open a new issue on GitHub", JSON.stringify(sup.links));
    const sChrome = await arabicChrome(p);
    check("en: support dialog has no Arabic UI text", sChrome.length === 0, sChrome.join(" | "));
    await p.keyboard.press("Escape");
    await closed(p, "#support-dialog");

    /* ---------- English bulk page ---------- */
    console.log("English bulk page");
    await p.goto(`${EN}bulk.html`);
    await ready(p);
    check("en/bulk: lang/dir and English title", (await p.getAttribute("html", "lang")) === "en" && (await p.getAttribute("html", "dir")) === "ltr" && /^Bulk conversion/.test(await p.title()));
    check("en/bulk: empty table text in English", (await p.textContent("#rows")).trim() === "Enter numbers in the box above, or import a CSV file, to see the results here.");
    if (REAL_LANGUAGES) check("en/bulk: English output by default", await p.isChecked('input[name="language"][value="en"]'));
    await p.selectOption("#unit", "SAR");
    await p.fill("#numbers", "1250.75\nabc\n2500");
    await p.waitForFunction(() => document.querySelector("#rows")?.dataset.rendered === "3", null, { timeout: 5000 }).catch(() => {});
    const cells = await p.$$eval("#rows td.c-text", (tds) => tds.map((td) => ({ text: td.textContent, dir: td.dir, lang: td.lang })));
    check("en/bulk: English rows and English error", cells.length === 3 && cells[0].text === tafqit("1250.75", { unit: "SAR", language: "en" }) && cells[1].text === "The number format is not valid." && cells[2].text === tafqit("2500", { unit: "SAR", language: "en" }), JSON.stringify(cells));
    check("en/bulk: counts", (await p.textContent("#count-ok")) === "2" && (await p.textContent("#count-err")) === "1" && (await p.textContent("#line-count")) === "3");
    const bChrome = await arabicChrome(p);
    check("en/bulk: no Arabic UI text outside lang=ar", bChrome.length === 0, bChrome.join(" | "));
    await p.click("#copy-all");
    await p.waitForTimeout(100);
    check("en/bulk: copy all, status in English", (await p.evaluate(() => navigator.clipboard.readText())).split("\n")[1] === "" && (await p.textContent("#copy-status")) === "Copied 3 lines; the lines with errors (1) were left empty.", await p.textContent("#copy-status"));
    const [download] = await Promise.all([p.waitForEvent("download"), p.click("#export")]);
    const csv = readFileSync(await download.path()).toString("utf8");
    check("en/bulk: CSV export with English header and error", download.suggestedFilename() === "amounts-in-words.csv" && csv.includes("Number,Cleaned number,Amount in words,Error") && csv.includes("The number format is not valid."), csv.slice(0, 120));
    check("en/bulk: export status in English", (await p.textContent("#copy-status")) === "Exported 3 rows to amounts-in-words.csv.");
    await p.setInputFiles("#csv-file", { name: "amounts.csv", mimeType: "text/csv", buffer: Buffer.from("Amount\r\n1500\r\n2\r\n", "utf8") });
    await p.waitForFunction(() => document.querySelector("#rows")?.dataset.rendered === "2", null, { timeout: 5000 }).catch(() => {});
    check("en/bulk: import status in English", (await p.textContent("#import-status")) === "Imported 2 numbers from “amounts.csv”.", await p.textContent("#import-status"));
    if (REAL_LANGUAGES) {
      await p.check('input[name="language"][value="ar"]');
      await p.check('input[name="hundreds"][value="مائة"]');
      await p.waitForTimeout(200);
      await p.waitForFunction(() => document.querySelector("#rows")?.dataset.rendered === "2", null, { timeout: 5000 }).catch(() => {});
      const ar = await p.$eval("#rows td.c-text", (td) => ({ text: td.textContent, dir: td.dir, lang: td.lang }));
      check("en/bulk: Arabic rows marked RTL when Arabic is chosen", ar.text === tafqit("1500", { unit: "SAR" }) && ar.dir === "rtl" && ar.lang === "ar", JSON.stringify(ar));
      await p.check('input[name="language"][value="en"]');
    }
    await p.setViewportSize({ width: 390, height: 844 });
    const label = await p.$eval("#rows td.c-in", (td) => getComputedStyle(td, "::before").content);
    check("en/bulk: mobile cell labels in English", label === '"Input: "', label);
    await p.setViewportSize({ width: 1280, height: 720 });

    /* ---------- English Excel page ---------- */
    if (existsSync(join(root, "en", "excel.html"))) {
      console.log("English Excel page");
      await p.goto(`${EN}excel.html`);
      await p.waitForLoadState("load");
      check("en/excel: lang/dir", (await p.getAttribute("html", "lang")) === "en" && (await p.getAttribute("html", "dir")) === "ltr");
      check("en/excel: formula with Language \"en\"", (await p.textContent("#formula")) === '=TAFQIT(A1, "USD", "", "", "Only", "", "en")');
      const ex = await p.$eval("[data-excel-example]", (e) => ({ text: e.textContent.trim(), ...e.dataset }));
      check("en/excel: example result matches the engine", ex.text === tafqit(ex.n, { unit: ex.unit, suffix: ex.suffix, language: ex.lang }), JSON.stringify(ex));
      const rows = await p.$$eval(".params tbody tr", (trs) => trs.map((tr) => tr.children[1].textContent));
      check("en/excel: the seven arguments, Language last", rows.join() === "Number,Unit,HundredsForm,Prefix,Suffix,Decimals,Language", rows.join());
      check("en/excel: Tafqit.bas download", (await p.$eval('a[download="Tafqit.bas"]', (a) => a.href)) === `${BASE}Tafqit.bas`);
      const enXlam = await p.$$eval('a[download="InstaWords.xlam"]', (as) => as.map((a) => a.href));
      check("en/excel: InstaWords.xlam is the main download (same-site file)", enXlam.length >= 1 && enXlam.every((h) => h === `${BASE}InstaWords.xlam`) && (await p.$eval(".hero-cta .btn-primary", (a) => a.getAttribute("download"))) === "InstaWords.xlam", JSON.stringify(enXlam));
      check("en/excel: Unblock and Trusted Location steps", /Unblock/.test(await p.textContent("#install")) && /Trusted Location/.test(await p.textContent("#trust")));
      const eChrome = await arabicChrome(p);
      check("en/excel: no Arabic UI text outside lang=ar", eChrome.length === 0, eChrome.join(" | "));
      await p.click('[data-copy-from="js-snippet"]');
      await p.waitForTimeout(100);
      check("en/excel: code copy button in English", (await p.textContent('[data-copy-from="js-snippet"]')).trim() === "Copied" && (await p.evaluate(() => navigator.clipboard.readText())).includes('language: "en"'));
    }
  } finally {
    await c.close();
  }
}

/** English pages at 1366px and 390px (TAFKET_SCREENSHOTS). */
async function englishScreenshots(dir) {
  mkdirSync(dir, { recursive: true });
  for (const [label, viewport] of [["390", { width: 390, height: 844 }], ["1366", { width: 1366, height: 900 }]]) {
    const c = await newContext({ viewport });
    const p = watch(await c.newPage());
    await p.goto(`${BASE}en/?n=1250.75&unit=SAR`);
    await ready(p);
    await p.evaluate(() => document.fonts.ready);
    await p.waitForTimeout(350);
    await p.screenshot({ path: join(dir, `index-${label}.png`), fullPage: true });
    await p.screenshot({ path: join(dir, `index-top-${label}.png`) });
    await p.fill("#number", "abc");
    await p.waitForTimeout(350);
    await p.locator(".cheque").screenshot({ path: join(dir, `index-error-${label}.png`) });
    await p.goto(`${BASE}en/?n=1250.75&unit=SAR&lang=ar`);
    await ready(p);
    await p.waitForTimeout(350);
    await p.locator(".tool").screenshot({ path: join(dir, `index-arabic-output-${label}.png`) });
    await p.goto(`${BASE}en/bulk.html`);
    await ready(p);
    await p.click("#sample");
    await p.waitForTimeout(200);
    await p.screenshot({ path: join(dir, `bulk-${label}.png`), fullPage: true });
    if (existsSync(join(root, "en", "excel.html"))) {
      await p.goto(`${BASE}en/excel.html`);
      await p.waitForLoadState("load");
      await p.screenshot({ path: join(dir, `excel-${label}.png`), fullPage: true });
    }
    await p.goto(`${BASE}en/#download`);
    await p.waitForSelector("#download-dialog[open] [data-release-notes-body]", { timeout: 5000 }).catch(() => {});
    await p.waitForTimeout(200);
    await p.screenshot({ path: join(dir, `download-dialog-${label}.png`) });
    await p.goto(`${BASE}en/#support`);
    await p.waitForTimeout(200);
    await p.screenshot({ path: join(dir, `support-dialog-${label}.png`) });
    await c.close();
  }
}

/** A unit the core can spell in `lang` (currencies first), falling back to "none". */
function unitFor(lang) {
  if (!REAL_LANGUAGES) return "SAR";
  return ["USD", "EUR", "SAR", "AED", "none"].find((u) => core.tryTafqit("1.5", { unit: u, language: lang }).ok) ?? "none";
}

function tryMessage(input, opts) {
  try {
    tafqit(input, opts);
    return "<no error>";
  } catch (e) {
    return e.message;
  }
}

console.log(`\n${passed} passed, ${failures.length} failed`);
if (failures.length) {
  for (const f of failures) console.error(`  - ${f}`);
  process.exit(1);
}
