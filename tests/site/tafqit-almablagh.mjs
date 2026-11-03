// End-to-end test of the Arabic landing page tafqit-almablagh.html in the built site (_site/),
// run by `npm run test:site` after smoke.mjs. Serves _site/ under /instawords/ like the real
// deployment and drives Chromium with Playwright.
import { createServer } from "node:http";
import { createRequire } from "node:module";
import { existsSync, readFileSync, statSync } from "node:fs";
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

const PAGE = "tafqit-almablagh.html";
const URL_PUBLIC = `https://fastkiwi.app/instawords/${PAGE}`;
const root = fileURLToPath(new URL("../../_site/", import.meta.url));
if (!existsSync(join(root, PAGE))) {
  console.error(`landing: _site/${PAGE} not found; run \`npm run build:site\` first.`);
  process.exit(1);
}
const core = await import(new URL("../../dist/index.js", import.meta.url));
const golden = JSON.parse(readFileSync(new URL("../golden/cases.json", import.meta.url), "utf8")).cases;
const G = (id) => golden.find((c) => c.id === id);
const unitsJson = JSON.parse(readFileSync(new URL("../../data/units.json", import.meta.url), "utf8"));
const FB = readFallback();
const STATIC_EXE = FB.setup.url;
const RELEASE_API = "https://api.github.com/repos/sarmadiq9090-coder/tafket/releases/latest";

const PREFIX = "/instawords/";
const MIME = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".webmanifest": "application/manifest+json; charset=utf-8",
  ".svg": "image/svg+xml",
  ".txt": "text/plain; charset=utf-8",
  ".woff2": "font/woff2",
};
const server = createServer((req, res) => {
  const path = decodeURIComponent(new URL(req.url, "http://x").pathname);
  if (!path.startsWith(PREFIX)) return void res.writeHead(404).end("outside sub-path");
  let file = normalize(join(root, path.slice(PREFIX.length)));
  if (!file.startsWith(root.endsWith(sep) ? root : root + sep) && file !== root) return void res.writeHead(403).end();
  if (existsSync(file) && statSync(file).isDirectory()) file = join(file, "index.html");
  if (!existsSync(file)) return void res.writeHead(404).end("not found");
  res.writeHead(200, { "Content-Type": MIME[extname(file)] ?? "application/octet-stream", "Cache-Control": "no-cache" });
  res.end(readFileSync(file));
});
await new Promise((r) => server.listen(0, "127.0.0.1", r));
const BASE = `http://127.0.0.1:${server.address().port}${PREFIX}`;

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
async function waitText(page, selector, expected, name) {
  try {
    await page.waitForFunction(([s, e]) => document.querySelector(s)?.textContent.trim() === e, [selector, expected], { timeout: 5000 });
    check(name, true);
  } catch {
    check(name, false, `expected «${expected}», got «${(await page.textContent(selector).catch(() => ""))?.trim()}»`);
  }
}
const norm = (s) => String(s).replace(/\s+/g, " ").trim();
const ready = (page) => page.waitForSelector("html[data-ready]", { timeout: 10000 });

const consoleErrors = [];
const externalRequests = [];
function watch(page) {
  page.on("request", (r) => {
    const url = r.url();
    if (!url.startsWith(new URL(BASE).origin) && !/^(blob|data):/.test(url) && url !== RELEASE_API) externalRequests.push(url);
  });
  page.on("console", (m) => {
    if (m.type() === "error" && !(m.location()?.url ?? "").startsWith("https://api.github.com/")) consoleErrors.push(`${page.url()}: ${m.text()}`);
  });
  page.on("pageerror", (e) => consoleErrors.push(`${page.url()}: ${e.message}`));
  return page;
}

/* ---------- static checks on the built file ---------- */

console.log(`landing page ${PAGE} (static)`);
const html = readFileSync(join(root, PAGE), "utf8");
const sw = readFileSync(join(root, "sw.js"), "utf8");
check("precached by the service worker", sw.includes(`"${PAGE}"`));
check("linked from index.html", readFileSync(join(root, "index.html"), "utf8").includes(`href="${PAGE}"`));
const winHrefs = [...html.matchAll(/<a\b[^>]*\bdata-windows-download\b[^>]*>/g)].map((m) => /href="([^"]+)"/.exec(m[0])?.[1]);
check("static Windows links download the fallback installer directly", winHrefs.length >= 3 && winHrefs.every((h) => h === STATIC_EXE), winHrefs.join(", "));
const versions = [...html.matchAll(/<span\b[^>]*\bdata-version\b[^>]*>([^<]*)<\/span>/g)].map((m) => m[1]);
check("static version labels show the fallback release", versions.length >= 3 && versions.every((v) => v === `v${FB.version}`), versions.join(", "));
check("no external stylesheet, script, font or image", ![...html.matchAll(/<(?:link|script|img)\b[^>]*\b(?:href|src)="(https?:)?\/\//g)].some((m) => !/rel="canonical"/.test(m[0])), "external resource in the page");

/* ---------- in the browser ---------- */

const browser = await chromium.launch();
try {
  const ctx = await browser.newContext({ permissions: ["clipboard-read", "clipboard-write"] });
  await ctx.route("https://api.github.com/**", (route) => route.abort("internetdisconnected"));
  const page = watch(await ctx.newPage());
  await page.goto(BASE + PAGE);
  await ready(page);

  console.log("head");
  check("lang=ar dir=rtl", (await page.getAttribute("html", "lang")) === "ar" && (await page.getAttribute("html", "dir")) === "rtl");
  const title = await page.title();
  check("title names تفقيط المبالغ and كتابة المبلغ بالحروف", title.includes("تفقيط المبالغ") && title.includes("كتابة المبلغ بالحروف"), title);
  const desc = await page.getAttribute('meta[name="description"]', "content");
  check("meta description (70–200 chars) mentions التفقيط", !!desc && desc.length >= 70 && desc.length <= 200 && desc.includes("تفقيط"), `${desc?.length}: ${desc}`);
  check("canonical", (await page.getAttribute('link[rel="canonical"]', "href")) === URL_PUBLIC);
  check("og:url = canonical, og:title, og:image", (await page.getAttribute('meta[property="og:url"]', "content")) === URL_PUBLIC && !!(await page.getAttribute('meta[property="og:title"]', "content")) && !!(await page.getAttribute('meta[property="og:image"]', "content")));
  const h1 = norm(await page.textContent("h1"));
  check("one H1 with the main phrase", (await page.locator("h1").count()) === 1 && h1.includes("تفقيط المبالغ") && h1.includes("كتابة المبلغ بالحروف"), h1);

  console.log("JSON-LD");
  const blocks = await page.$$eval('script[type="application/ld+json"]', (els) => els.map((e) => e.textContent));
  let nodes = [];
  try {
    nodes = blocks.flatMap((t) => JSON.parse(t)["@graph"] ?? []);
    check("JSON-LD parses", nodes.length > 0);
  } catch (err) {
    check("JSON-LD parses", false, err.message);
  }
  const app = nodes.find((n) => n["@type"] === "WebApplication");
  check("WebApplication: free, publisher, direct installer downloadUrl", app?.offers?.price === "0" && app?.publisher?.["@id"] === "https://fastkiwi.app/#org" && app?.downloadUrl === STATIC_EXE, JSON.stringify(app?.downloadUrl));
  const faq = nodes.find((n) => n["@type"] === "FAQPage");
  check("FAQPage for this URL", faq?.url === URL_PUBLIC && Array.isArray(faq?.mainEntity) && faq.mainEntity.length >= 5);
  const visible = await page.$$eval(".faq details", (ds) => ds.map((d) => ({ q: d.querySelector("summary").textContent, a: d.querySelector("summary + div").textContent })));
  const ldFaq = (faq?.mainEntity ?? []).map((q) => ({ q: q.name, a: q.acceptedAnswer?.text }));
  const faqDiff = visible.map((v, i) => [v, ldFaq[i]]).filter(([v, l]) => !l || norm(v.q) !== norm(l.q) || norm(v.a) !== norm(l.a));
  check(`FAQ JSON-LD matches the visible FAQ word for word (${visible.length})`, visible.length === ldFaq.length && faqDiff.length === 0, faqDiff.map(([v]) => norm(v.q)).join(" | "));
  const crumbs = nodes.find((n) => n["@type"] === "BreadcrumbList");
  check("BreadcrumbList ends at this page", crumbs?.itemListElement?.at(-1)?.item === URL_PUBLIC);

  console.log("converter");
  await page.selectOption("#unit", "SAR");
  const g32 = G("G32");
  await page.fill("#number", g32.input);
  await waitText(page, "#result", g32.expected, `${g32.input} SAR → golden ${g32.id}`);
  await waitText(page, "#cleaned", "1,250.75", "cleaned number shown");
  const g40 = G("G40");
  await page.click('[data-preset="only"]');
  await page.fill("#number", g40.input);
  await waitText(page, "#result", g40.expected, `«فقط … لا غير» → golden ${g40.id}`);
  await page.click('[data-preset="none"]');
  const g36 = G("G36");
  await page.fill("#number", g36.input);
  await waitText(page, "#result", g36.expected, `Arabic-Indic digits → golden ${g36.id}`);
  await page.click("#copy");
  await page.waitForTimeout(100);
  check("copy to clipboard", (await page.evaluate(() => navigator.clipboard.readText())) === g36.expected);
  await page.goto(`${BASE}${PAGE}?n=5.250&unit=KWD`);
  await ready(page);
  await waitText(page, "#result", G("G37").expected, "deep link ?n=&unit= → golden G37");
  await page.selectOption("#unit", "SAR");

  console.log("content");
  const examples = await page.$$eval("[data-example]", (els) => els.map((e) => ({ ...e.dataset, text: e.textContent.trim() })));
  const wrong = examples.filter((e) => e.text !== core.tafqit(e.n, { unit: e.unit, prefix: e.prefix ?? "", suffix: e.suffix ?? "", hundredsForm: e.h ?? "مائة" }));
  check(`every worked example is the engine's output (${examples.length})`, examples.length >= 8 && wrong.length === 0, wrong.map((e) => `${e.n} ${e.unit}: «${e.text}»`).join("; "));
  const goldenTexts = new Set(golden.map((c) => c.expected));
  check("most examples are golden cases", examples.filter((e) => goldenTexts.has(e.text)).length >= 6);
  const rows = await page.$$eval("#currency-table tbody tr", (trs) => trs.map((tr) => [...tr.cells].map((c) => c.textContent.trim())));
  const badRows = rows.filter(([code, label, sub]) => {
    const u = unitsJson.units.find((x) => x.id === code);
    return !u || u.label !== label || sub !== `${u.subunit?.ratio} ${u.subunit?.singular}`;
  });
  check(`currency table matches units.json (${rows.length} rows)`, rows.length >= 10 && badRows.length === 0, badRows.map((r) => r.join(" / ")).join("; "));
  const sections = await page.$$eval("main h2, main h3", (hs) => hs.map((h) => h.textContent).join(" | "));
  check("sections: cheque guide, currencies, فقط … لا غير, مائة/مئة, Excel, Windows", ["الشيكات", "العملات وأجزاؤها", "فقط … لا غير", "«مائة» أم «مئة»", "Excel", "ويندوز"].every((s) => sections.includes(s)), sections);
  check("links to the Excel page", (await page.locator('main a[href="excel.html"]').count()) >= 1);

  console.log("links");
  const links = await page.$$eval("a[href]", (as) => as.map((a) => ({ href: a.href, gh: a.hasAttribute("data-github"), dl: a.hasAttribute("data-windows-download"), text: a.textContent.trim() })));
  const gh = links.filter((l) => /^https?:\/\/([^/]+\.)?github\.com\//.test(l.href));
  const files = gh.filter((l) => /\/releases\/download\/v[\d.]+\/InstaWords_[\d.]+_x64-(setup\.exe|portable\.zip)$/.test(l.href));
  const bad = gh.filter((l) => !files.includes(l) && !(l.gh && l.text.includes("GitHub")));
  check("every github.com link is a direct release file or an explicit GitHub (source) link", bad.length === 0 && files.every((l) => l.dl), bad.map((l) => `${l.text} → ${l.href}`).join(" | "));
  check("Windows download buttons point at the installer (API unreachable → fallback)", links.filter((l) => l.dl).length >= 3 && links.filter((l) => l.dl).every((l) => l.href === STATIC_EXE));
  await page.click("#windows [data-release-notes]");
  check("portable/release-notes link opens the on-site #download dialog", await page.isVisible("#download-dialog[open]"));
  check("dialog offers the portable zip directly", (await page.getAttribute("#download-dialog [data-portable-download]", "href")) === FB.portable?.url);
  await page.keyboard.press("Escape");

  console.log("offline");
  await page.goto(BASE + PAGE);
  await ready(page);
  await page.evaluate(async () => { await navigator.serviceWorker.ready; });
  await page.waitForFunction(() => !!navigator.serviceWorker.controller, null, { timeout: 10000 }).catch(() => {});
  await ctx.setOffline(true);
  await page.reload();
  await ready(page);
  await page.selectOption("#unit", "SAR");
  await page.fill("#number", g32.input);
  await waitText(page, "#result", g32.expected, "offline: the page loads from the cache and converts");
  await ctx.setOffline(false);
  await ctx.close();

  console.log("layout");
  const narrow = await browser.newContext({ viewport: { width: 360, height: 740 } });
  await narrow.route("https://api.github.com/**", (route) => route.abort("internetdisconnected"));
  const np = watch(await narrow.newPage());
  await np.goto(BASE + PAGE);
  await ready(np);
  const [sw2, cw] = await np.evaluate(() => [document.documentElement.scrollWidth, document.documentElement.clientWidth]);
  check("no horizontal scroll at 360px", sw2 <= cw, `${sw2} > ${cw}`);
  if (process.env.TAFKET_SCREENSHOTS) {
    await np.evaluate(() => document.fonts.ready);
    await np.screenshot({ path: join(process.env.TAFKET_SCREENSHOTS, "tafqit-almablagh-mobile.png"), fullPage: true });
    await np.setViewportSize({ width: 1280, height: 900 });
    await np.screenshot({ path: join(process.env.TAFKET_SCREENSHOTS, "tafqit-almablagh-desktop.png"), fullPage: true });
  }
  await narrow.close();

  check("no console errors", consoleErrors.length === 0, consoleErrors.join(" | "));
  check("no requests outside the site", externalRequests.length === 0, [...new Set(externalRequests)].join(" | "));
} catch (err) {
  failures.push(`unexpected: ${err.stack ?? err}`);
  console.error(err);
} finally {
  await browser.close();
  server.close();
}

console.log(`\n${passed} passed, ${failures.length} failed`);
if (failures.length) {
  for (const f of failures) console.error(`  - ${f}`);
  process.exit(1);
}
