// Shared by the release scripts, scripts/build-site.mjs and tests/site/smoke.mjs:
// the repository, the asset naming the app's updater relies on, and site/release-fallback.json,
// the single source for the release the website links to without JavaScript or when GitHub is
// unreachable (static download links, JSON-LD downloadUrl, release.js FALLBACK, smoke tests).
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

export const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
export const REPO = "sarmadiq9090-coder/InstaWords";
export const REPO_URL = `https://github.com/${REPO}`;
export const LATEST_API = `https://api.github.com/repos/${REPO}/releases/latest`;
export const DOWNLOAD_PREFIX = `${REPO_URL}/releases/download/`;
export const FALLBACK_PATH = join(ROOT, "site", "release-fallback.json");

// Same patterns as site/assets/release.js and the Windows app's updater: keep the asset names.
export const SETUP_RE = /^InstaWords_([\d.]+)_x64-setup\.exe$/;
export const PORTABLE_RE = /^InstaWords_([\d.]+)_x64-portable\.zip$/;
export const VERSION_RE = /^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/;

export const setupName = (v) => `InstaWords_${v}_x64-setup.exe`;
export const portableName = (v) => `InstaWords_${v}_x64-portable.zip`;
export const assetUrl = (v, name) => `${DOWNLOAD_PREFIX}v${v}/${name}`;

/** Compares "1.2.3" / "1.2.3-rc.1" versions: <0, 0, >0. A pre-release sorts before its release. */
export function compareVersions(a, b) {
  const split = (v) => {
    const [core, pre = ""] = String(v).replace(/^v/i, "").split("-", 2);
    return { nums: core.split(".").map((n) => Number.parseInt(n, 10) || 0), pre };
  };
  const x = split(a);
  const y = split(b);
  for (let i = 0; i < Math.max(x.nums.length, y.nums.length); i++) {
    const d = (x.nums[i] ?? 0) - (y.nums[i] ?? 0);
    if (d) return d;
  }
  if (x.pre === y.pre) return 0;
  if (!x.pre) return 1;
  if (!y.pre) return -1;
  return x.pre < y.pre ? -1 : 1;
}

/** Throws when `fb` is not a well-formed fallback release (see site/release-fallback.json). */
export function validateFallback(fb) {
  const problems = [];
  const need = (ok, msg) => ok || problems.push(msg);
  need(fb && typeof fb === "object", "not an object");
  if (problems.length) throw new Error(`release-fallback.json: ${problems.join("; ")}`);
  need(VERSION_RE.test(String(fb.version)), `version "${fb.version}" is not x.y.z`);
  need(fb.tag === `v${fb.version}`, `tag "${fb.tag}" must be "v${fb.version}"`);
  need(!Number.isNaN(Date.parse(fb.date)), `date "${fb.date}" is not a date`);
  for (const [key, re, nameOf, required] of [
    ["setup", SETUP_RE, setupName, true],
    ["portable", PORTABLE_RE, portableName, false],
  ]) {
    const a = fb[key];
    if (!a) {
      need(!required, `${key} is missing`);
      continue;
    }
    need(re.test(a.name ?? "") && a.name === nameOf(fb.version), `${key}.name "${a.name}" must be ${nameOf(fb.version)}`);
    need(a.url === assetUrl(fb.version, a.name), `${key}.url must be ${assetUrl(fb.version, a.name)}`);
    need(Number.isInteger(a.size) && a.size > 0, `${key}.size must be a positive integer`);
    need(/^[0-9a-f]{64}$/.test(a.sha256 ?? ""), `${key}.sha256 must be 64 lowercase hex digits`);
  }
  if (problems.length) throw new Error(`release-fallback.json: ${problems.join("; ")}`);
  return fb;
}

export function readFallback(path = FALLBACK_PATH) {
  return validateFallback(JSON.parse(readFileSync(path, "utf8")));
}
