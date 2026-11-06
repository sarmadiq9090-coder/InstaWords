// Stages the Microsoft Store package (MSIX) layout from the app's build output:
//
//   node scripts/msix-stage.mjs --app windows/InstaWords.App/bin/Release/net48 --out out/msix/stage
//       [--version 2.1.0] [--identity windows/packaging/msix/identity.json]
//
// Copies InstaWords.exe, .exe.config, InstaWords.Core.dll and Licenses/, the visual assets and the
// package strings (windows/packaging/msix/Assets, Strings), and writes AppxManifest.xml from the
// template with the Partner Center identity and the Store version as x.y.z.0 (identity.json storeVersion
// when set, otherwise package.json's version; --version overrides both).
// windows/packaging/msix/build-msix.ps1 runs this, then MakePri and MakeAppx (Windows SDK).
// Prints one JSON line: { name, publisher, version, file, placeholder }.
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { ROOT } from "./lib/release.mjs";
import { APP_FILES, identityErrors, isPlaceholder, packageFileName, packageVersion, renderManifest, storeVersion } from "./lib/msix.ts";

const MSIX = join(ROOT, "windows", "packaging", "msix");
const args = process.argv.slice(2);
const option = (name, fallback) => {
  const i = args.indexOf(name);
  return i >= 0 && args[i + 1] ? args[i + 1] : fallback;
};
const fail = (msg) => {
  console.error(`msix-stage: ${msg}`);
  process.exit(1);
};

const app = resolve(option("--app", join(ROOT, "windows", "InstaWords.App", "bin", "Release", "net48")));
const out = resolve(option("--out", join(ROOT, "out", "msix", "stage")));
const identityPath = resolve(option("--identity", join(MSIX, "identity.json")));

const identity = JSON.parse(readFileSync(identityPath, "utf8"));
const errors = identityErrors(identity);
if (errors.length) fail(`${identityPath}:\n  ${errors.join("\n  ")}`);
let version;
try {
  version = option("--version", "") || storeVersion(identity, JSON.parse(readFileSync(join(ROOT, "package.json"), "utf8")).version);
  packageVersion(version);
} catch (e) {
  fail(e.message);
}
for (const f of [...APP_FILES, "Licenses"]) if (!existsSync(join(app, f))) fail(`missing ${join(app, f)}: build the app first (dotnet build windows/InstaWords.App -c Release)`);

rmSync(out, { recursive: true, force: true });
mkdirSync(out, { recursive: true });
for (const f of APP_FILES) cpSync(join(app, f), join(out, f));
cpSync(join(app, "Licenses"), join(out, "Licenses"), { recursive: true });
cpSync(join(MSIX, "Assets"), join(out, "Assets"), { recursive: true, filter: (src) => !src.endsWith(".md") });
cpSync(join(MSIX, "Strings"), join(out, "Strings"), { recursive: true });
const manifest = renderManifest(readFileSync(join(MSIX, "AppxManifest.xml"), "utf8"), identity, version);
writeFileSync(join(out, "AppxManifest.xml"), manifest);

// Every logo the manifest names must exist in at least one scale/targetsize variant.
const assets = readdirSync(join(out, "Assets"));
for (const m of manifest.matchAll(/Assets\\([\w]+)\.png/g)) {
  if (!assets.some((a) => a.startsWith(`${m[1]}.`))) fail(`no Assets/${m[1]}.*.png for the manifest: run windows/packaging/msix/make-assets.py`);
}

const placeholder = isPlaceholder(identity);
if (placeholder) {
  const msg = "identity.json holds test values (placeholder): fine for CI and sideloading, but the Store will reject this package. See docs/distribution/microsoft-store.md.";
  console.error(process.env.GITHUB_ACTIONS ? `::warning::${msg}` : `warning: ${msg}`);
}
console.log(JSON.stringify({ name: identity.Name, publisher: identity.Publisher, version: packageVersion(version), file: packageFileName(version), placeholder }));
