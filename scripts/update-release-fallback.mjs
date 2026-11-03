// Refreshes site/release-fallback.json (the release the website links to without JavaScript or
// when GitHub is unreachable) from the latest GitHub release. Run after each release:
//
//   node scripts/update-release-fallback.mjs            # or: npm run release:fallback
//   GITHUB_TOKEN=... node scripts/update-release-fallback.mjs   (optional: higher API rate limit)
//
// Options:
//   --force           write even when the latest release is not newer than the current file
//   --dry-run         print what would be written, change nothing
//   --from <file>     read the release JSON from a file instead of the API (tests, offline use)
//
// The SHA-256 comes from each asset's `digest` ("sha256:…", set by GitHub for every upload);
// when an asset has none, the release's SHA256SUMS.txt is used. Exits 0 with a message when
// GitHub cannot be reached, so it is safe in scheduled jobs; exits 1 on malformed data.
import { readFileSync, writeFileSync } from "node:fs";
import { relative } from "node:path";
import {
  DOWNLOAD_PREFIX,
  FALLBACK_PATH,
  LATEST_API,
  PORTABLE_RE,
  ROOT,
  SETUP_RE,
  compareVersions,
  readFallback,
  validateFallback,
} from "./lib/release.mjs";

const args = process.argv.slice(2);
const flag = (name) => args.includes(name);
const option = (name) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : undefined;
};
const FORCE = flag("--force");
const DRY = flag("--dry-run");
const FROM = option("--from");
const TIMEOUT = 15000;
const rel = relative(ROOT, FALLBACK_PATH);

const fail = (msg) => {
  console.error(`update-release-fallback: ${msg}`);
  process.exit(1);
};
const offline = (msg) => {
  console.log(`update-release-fallback: GitHub not reachable (${msg}); ${rel} left unchanged.`);
  process.exit(0);
};

let token = process.env.GITHUB_TOKEN || process.env.GH_TOKEN || "";

function headers() {
  const h = { Accept: "application/vnd.github+json", "User-Agent": "instawords-release-fallback", "X-GitHub-Api-Version": "2022-11-28" };
  if (token) h.Authorization = `Bearer ${token}`;
  return h;
}

async function get(url, as = "json") {
  const res = await fetch(url, { headers: as === "json" ? headers() : { "User-Agent": "instawords-release-fallback" }, signal: AbortSignal.timeout(TIMEOUT) });
  if (res.status === 401 && token && as === "json") {
    // The repository is public: a stale or foreign token should not stop the refresh.
    console.warn("update-release-fallback: GITHUB_TOKEN was rejected (HTTP 401); retrying without it.");
    token = "";
    return get(url, as);
  }
  if (!res.ok) {
    const err = new Error(`HTTP ${res.status} for ${url}`);
    err.status = res.status;
    throw err;
  }
  return as === "json" ? res.json() : res.text();
}

async function loadRelease() {
  if (FROM) return JSON.parse(readFileSync(FROM, "utf8"));
  try {
    return await get(LATEST_API);
  } catch (err) {
    // No network, DNS failure, timeout, rate limit or GitHub outage: nothing to do now.
    if (err.status === undefined || err.status === 403 || err.status === 429 || err.status >= 500) offline(err.cause?.code ?? err.message);
    if (err.status === 404) offline("no published release yet (HTTP 404)");
    throw err;
  }
}

/** SHA256SUMS.txt of the release (`<hash>  <file>` lines), or an empty map. */
async function checksums(release) {
  const sums = release.assets.find((a) => a?.name === "SHA256SUMS.txt");
  if (!sums) return new Map();
  let text;
  try {
    text = FROM ? "" : await get(sums.browser_download_url, "text");
  } catch (err) {
    console.warn(`update-release-fallback: could not read SHA256SUMS.txt (${err.message})`);
    return new Map();
  }
  const map = new Map();
  for (const line of text.split(/\r?\n/)) {
    const m = /^([0-9a-fA-F]{64})\s+\*?(.+?)\s*$/.exec(line);
    if (m) map.set(m[2], m[1].toLowerCase());
  }
  return map;
}

function pick(release, re, sums) {
  const a = release.assets.find((x) => x && re.test(String(x.name)));
  if (!a) return null;
  if (!String(a.browser_download_url).startsWith(DOWNLOAD_PREFIX)) fail(`${a.name}: unexpected download URL ${a.browser_download_url}`);
  const digest = /^sha256:([0-9a-f]{64})$/i.exec(String(a.digest ?? ""))?.[1]?.toLowerCase() ?? sums.get(a.name);
  if (!digest) fail(`${a.name}: no SHA-256 (no asset digest and no SHA256SUMS.txt entry)`);
  return { name: a.name, url: a.browser_download_url, size: a.size, sha256: digest };
}

const release = await loadRelease();
if (!release || !Array.isArray(release.assets)) fail("the release JSON has no assets");
if (release.draft || release.prerelease) offline(`latest release ${release.tag_name} is a draft or pre-release`);
const tag = String(release.tag_name ?? "");
const version = tag.replace(/^v/i, "");
const sums = await checksums(release);
const setup = pick(release, SETUP_RE, sums);
if (!setup) fail(`release ${tag} has no InstaWords_<version>_x64-setup.exe asset`);
const portable = pick(release, PORTABLE_RE, sums);

let next;
try {
  next = validateFallback({ version, tag, date: release.published_at, setup, ...(portable ? { portable } : {}) });
} catch (err) {
  fail(err.message);
}

let current = null;
try {
  current = readFallback();
} catch (err) {
  console.warn(`update-release-fallback: current ${rel} unreadable (${err.message}); it will be replaced.`);
}

const text = `${JSON.stringify(next, null, 2)}\n`;
if (current && !FORCE && compareVersions(next.version, current.version) <= 0) {
  const same = JSON.stringify(current) === JSON.stringify(next);
  console.log(`update-release-fallback: ${rel} already at v${current.version}${same ? "" : ` (latest release v${next.version} is not newer; --force to overwrite)`}; nothing to do.`);
  process.exit(0);
}
if (DRY) {
  console.log(`update-release-fallback: would write ${rel} (v${current?.version ?? "?"} → v${next.version}):\n${text}`);
  process.exit(0);
}
writeFileSync(FALLBACK_PATH, text);
console.log(`update-release-fallback: ${rel} → v${next.version} (was v${current?.version ?? "?"}). Rebuild the site: npm run test:site`);
