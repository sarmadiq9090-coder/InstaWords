// Prepares a release: bumps the version everywhere it is written by hand, then prints the checklist.
//
//   npm run release:prepare -- 2.1.0            # or: node scripts/release-prepare.mjs 2.1.0
//   npm run release:prepare -- 2.1.0 --dry-run  # show the changes, write nothing
//
// Updated: package.json and package-lock.json (the version every build reads: the Windows app,
// the installer, the release tag), the default <Version> of the app project (local builds; CI
// passes -p:Version), and the build examples in windows/README.md and the Inno Setup script.
// Not updated on purpose: site/release-fallback.json and packaging/ must name a release that
// exists, so they are refreshed after the release is published (npm run release:fallback). See RELEASING.md.
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { ROOT, VERSION_RE, compareVersions, readFallback } from "./lib/release.mjs";

const args = process.argv.slice(2);
const DRY = args.includes("--dry-run");
const version = args.find((a) => !a.startsWith("--"))?.replace(/^v/i, "");
const fail = (msg) => {
  console.error(`release-prepare: ${msg}`);
  process.exit(1);
};
if (!version) fail("usage: npm run release:prepare -- <version>   (e.g. 2.1.0)");
if (!VERSION_RE.test(version)) fail(`"${version}" is not a version like 2.1.0 or 2.1.0-rc.1`);

const at = (...p) => join(ROOT, ...p);
const pkg = JSON.parse(readFileSync(at("package.json"), "utf8"));
const previous = pkg.version;
if (compareVersions(version, previous) <= 0) fail(`${version} is not newer than the current version ${previous}`);

const changes = [];
function edit(path, transform, { required = true } = {}) {
  const file = at(path);
  if (!existsSync(file)) {
    if (required) fail(`${path} not found`);
    return;
  }
  const before = readFileSync(file, "utf8");
  const after = transform(before);
  if (after === before) {
    if (required) fail(`${path}: nothing to update (expected the version ${previous} in it)`);
    console.warn(`release-prepare: warning: ${path}: version not found, left as is`);
    return;
  }
  changes.push(path);
  if (!DRY) writeFileSync(file, after);
}

const esc = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
const V = esc(previous);

// package.json: only the top-level "version" field (keeps formatting and key order).
edit("package.json", (s) => s.replace(new RegExp(`^(  "version": )"${V}"`, "m"), `$1"${version}"`));
// package-lock.json: the root's "version" and packages[""].version.
edit("package-lock.json", (s) => {
  const lock = JSON.parse(s);
  if (lock.version !== previous && lock.packages?.[""]?.version !== previous) return s;
  lock.version = version;
  if (lock.packages?.[""]) lock.packages[""].version = version;
  return `${JSON.stringify(lock, null, 2)}\n`;
});
// Default version of local (non-CI) builds of the app.
edit("windows/InstaWords.App/InstaWords.App.csproj", (s) => s.replace(new RegExp(`(<Version Condition="'\\$\\(Version\\)' == ''">)${V}(</Version>)`), `$1${version}$2`), { required: false });
// Build examples.
edit("windows/README.md", (s) => s.replace(new RegExp(`(/DAppVersion=)${V}( windows\\\\installer\\\\InstaWords\\.iss.*InstaWords_)${V}(_x64-setup\\.exe)`), `$1${version}$2${version}$3`), { required: false });
edit("windows/installer/InstaWords.iss", (s) => s.replace(new RegExp(`(;   iscc /DAppVersion=)${V}( )`), `$1${version}$2`), { required: false });

const fallback = readFallback();
const tag = `v${version}`;
console.log(`
release-prepare: ${previous} → ${version}${DRY ? " (dry run: nothing written)" : ""}
${changes.map((c) => `  updated ${c}`).join("\n")}

Checklist (RELEASING.md has the details):
  [ ] 1. Tests:            npm run test:all   (and npm run test:dotnet if the .NET SDK is installed)
  [ ] 2. Commit and push:  git commit -am "Release ${tag}" && git push
  [ ] 3. Wait for the "Windows (native app)" workflow on that commit to pass (build, UI tests, screenshots).
  [ ] 4. Publish:          Actions → "Windows (native app)" → Run workflow → tick "release"
                           (creates tag ${tag} and the GitHub release with the setup, the portable zip,
                           SHA256SUMS.txt, winget-manifests.zip and instawords.json; unsigned, see
                           docs/CODE-SIGNING.md)
  [ ] 5. Check the release page: 5 assets, notes, and (optionally) install over an older version.
  [ ] 6. Website fallback: npm run release:fallback   (site/release-fallback.json v${fallback.version} → ${tag},
                           packaging/ follows), then npm run test:site, commit and push.
                           The FastKiwi site's daily sync also picks the new release up.
  [ ] 7. winget / Scoop:   docs/PACKAGING.md (wingetcreate submit; copy instawords.json into the bucket).
  [ ] 8. Real devices:     docs/TESTING-CHECKLIST.md for anything that changed in the app.
`);
if (!DRY) console.log(`Next: review with "git diff" (${changes.length} files).`);
