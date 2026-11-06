// Prints the version of the Microsoft Store build (x.y.z), for the store-package CI job:
//
//   node scripts/msix-version.mjs            → 2.2.1
//
// It is windows/packaging/msix/identity.json "storeVersion" when set, otherwise package.json's version.
// Fails when storeVersion is not x.y.z or is lower than package.json's version (scripts/lib/msix.ts storeVersion).
// The job builds the app with -p:Version=<this> (About shows it) and the MSIX as <this>.0.
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { ROOT } from "./lib/release.mjs";
import { storeVersion } from "./lib/msix.ts";

const identity = JSON.parse(readFileSync(join(ROOT, "windows", "packaging", "msix", "identity.json"), "utf8"));
const { version } = JSON.parse(readFileSync(join(ROOT, "package.json"), "utf8"));
try {
  console.log(storeVersion(identity, version));
} catch (e) {
  console.error(`msix-version: ${e.message}`);
  process.exit(1);
}
