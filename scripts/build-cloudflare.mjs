// Copies the built static site (_site/) into _cf/tafqit/ so Workers Static Assets
// serves it under the /tafqit/ path. Run after `npm run build:site`.
import { cpSync, existsSync, rmSync } from "node:fs";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("..", import.meta.url));
if (!existsSync(root + "_site/index.html")) {
  console.error("_site/ is missing — run `npm run build:site` first.");
  process.exit(1);
}
rmSync(root + "_cf", { recursive: true, force: true });
cpSync(root + "_site", root + "_cf/tafqit", { recursive: true });
rmSync(root + "_cf/tafqit/CNAME", { force: true });
console.log("_cf/tafqit ready");
