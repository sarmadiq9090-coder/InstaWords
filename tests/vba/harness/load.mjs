// Loads the generated VBA modules into the harness.
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { loadVba } from "./vba.mjs";

export const vbaDir = fileURLToPath(new URL("../../../vba/", import.meta.url));
export const BAS_FILES = ["Tafqit.bas", "TafqitTests.bas"];

export function loadTafqitVba(options) {
  return loadVba(
    BAS_FILES.map((file) => ({ file, text: readFileSync(vbaDir + file, "latin1") })),
    options,
  );
}
