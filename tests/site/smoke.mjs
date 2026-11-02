// End-to-end smoke test of the built site in _site/ (run via `npm run test:site`).
// Serves _site/ under the sub-path /tafqit/ (like the real deployment) and drives Chromium with Playwright.
// Set TAFKET_SCREENSHOTS=<dir> to also save mobile (375px) and desktop (1280px) screenshots.
import { createServer } from "node:http";
import { createRequire } from "node:module";
import { existsSync, mkdirSync, readFileSync, statSync } from "node:fs";
import { extname, join, normalize, sep } from "node:path";
import { fileURLToPath } from "node:url";

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
const { tafqit } = await import(new URL("../../dist/index.js", import.meta.url));

const PREFIX = "/tafqit/";
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
    if (!url.startsWith(new URL(BASE).origin) && !/^(blob|data):/.test(url)) externalRequests.push(url);
  });
  page.on("console", (m) => {
    if (m.type() === "error") consoleErrors.push(`${page.url()}: ${m.text()}`);
  });
  page.on("pageerror", (e) => consoleErrors.push(`${page.url()}: ${e.message}`));
  return page;
}
const ready = (page) => page.waitForSelector("html[data-ready]", { timeout: 10000 });
const rowCount = (page) => page.locator("#rows tr:not(.empty-row)").count();

const browser = await chromium.launch();
try {
  /* ---------- single page ---------- */
  console.log("single page");
  const ctx = await browser.newContext({ permissions: ["clipboard-read", "clipboard-write"], acceptDownloads: true });
  const page = watch(await ctx.newPage());
  await page.goto(BASE);
  await ready(page);

  check("lang/dir", (await page.getAttribute("html", "lang")) === "ar" && (await page.getAttribute("html", "dir")) === "rtl");
  await page.evaluate(() => document.fonts.ready);
  check("self-hosted Plex Arabic font loaded", await page.evaluate(() => document.fonts.check('700 16px "Plex Arabic"', "تفقيط")));
  const ld = await page.$$eval('script[type="application/ld+json"]', (els) => els.map((e) => e.textContent));
  let app;
  try {
    app = ld.flatMap((t) => JSON.parse(t)["@graph"] ?? []).find((n) => n["@type"] === "WebApplication");
  } catch {}
  check("JSON-LD WebApplication published by FastKiwi", app?.publisher?.["@id"] === "https://fastkiwi.app/#org" && app?.url === "https://fastkiwi.app/tafqit/");
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
  await waitText(page, "#cleaned", "1250.75", "cleaned number shown");

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
  await ctx.setOffline(false);
  await ctx.close();

  /* ---------- layout: no horizontal scroll at 360px ---------- */
  console.log("layout");
  const narrow = await browser.newContext({ viewport: { width: 360, height: 740 } });
  const np = watch(await narrow.newPage());
  for (const p of ["", "bulk.html", "excel.html"]) {
    await np.goto(BASE + p);
    await np.waitForLoadState("load");
    if (p === "bulk.html") {
      await ready(np);
      await np.click("#sample");
      await np.waitForTimeout(150);
    }
    const [sw, cw] = await np.evaluate(() => [document.documentElement.scrollWidth, document.documentElement.clientWidth]);
    check(`no horizontal scroll at 360px: ${p || "index.html"}`, sw <= cw, `${sw} > ${cw}`);
  }
  await narrow.close();

  /* ---------- screenshots ---------- */
  const shots = process.env.TAFKET_SCREENSHOTS;
  if (shots) {
    mkdirSync(shots, { recursive: true });
    {
      const scheme = "light";
      for (const [label, viewport] of [["mobile", { width: 375, height: 812 }], ["desktop", { width: 1280, height: 900 }]]) {
        const c = await browser.newContext({ viewport, colorScheme: scheme });
        const p = watch(await c.newPage());
        await p.goto(`${BASE}?n=1250.75&unit=SAR`);
        await ready(p);
        await p.evaluate(() => document.fonts.ready);
        await p.screenshot({ path: join(shots, `index-${label}-${scheme}.png`), fullPage: true });
        await p.fill("#number", "abc");
        await p.locator(".cheque").screenshot({ path: join(shots, `index-error-${label}-${scheme}.png`) });
        await p.goto(`${BASE}bulk.html`);
        await ready(p);
        await p.click("#sample");
        await p.waitForTimeout(200);
        await p.screenshot({ path: join(shots, `bulk-${label}-${scheme}.png`), fullPage: true });
        await p.goto(`${BASE}excel.html`);
        await p.waitForLoadState("load");
        await p.screenshot({ path: join(shots, `excel-${label}-${scheme}.png`), fullPage: true });
        await c.close();
      }
    }
    console.log(`screenshots → ${shots}`);
  }

  // Light theme only: a dark-mode preference must not change the page.
  const dark = await browser.newContext({ colorScheme: "dark" });
  const dp = watch(await dark.newPage());
  await dp.goto(BASE);
  await ready(dp);
  const bg = await dp.evaluate(() => getComputedStyle(document.body).backgroundColor);
  check("light theme even when the system prefers dark", bg === "rgb(255, 255, 255)", bg);
  await dark.close();

  check("no console errors", consoleErrors.length === 0, consoleErrors.join(" | "));
  check("no requests outside the site (fully offline-capable)", externalRequests.length === 0, [...new Set(externalRequests)].join(" | "));
} catch (err) {
  failures.push(`unexpected: ${err.stack ?? err}`);
  console.error(err);
} finally {
  await browser.close();
  server.close();
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
