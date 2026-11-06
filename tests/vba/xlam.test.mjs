// The Excel add-in InstaWords.xlam (scripts/build-xlam.mjs): read back with the independent readers
// in harness/xlam-read.mjs and compared with vba/Tafqit.bas. When OLETOOLS_PYTHON names a Python
// with oletools installed (CI does this), olevba checks the file as well.
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { buildXlam, ovbaCompress, ADDIN_CONTENT_TYPE, VBA_REL_TYPE, PROJECT_NAME } from "../../scripts/build-xlam.mjs";
import { readZip, readCfb, ovbaDecompress, parseDir, ovbaDecrypt } from "./harness/xlam-read.mjs";
import { vbaDir } from "./harness/load.mjs";

const bas = readFileSync(vbaDir + "Tafqit.bas");
const xlam = buildXlam(bas.toString("latin1"), { version: "0.0.0-test" });
const zip = readZip(xlam);
const cfb = readCfb(zip.get("xl/vbaProject.bin"));
const project = cfb.streams.get("PROJECT").toString("latin1");
const dir = parseDir(ovbaDecompress(cfb.streams.get("VBA/dir")));

test("xlam: reproducible (same input, same bytes)", () => {
  assert.ok(buildXlam(bas.toString("latin1"), { version: "0.0.0-test" }).equals(xlam));
});

test("xlam: package parts, add-in content type and vbaProject relationship", () => {
  assert.deepEqual([...zip.keys()].sort(), [
    "[Content_Types].xml",
    "_rels/.rels",
    "docProps/core.xml",
    "xl/_rels/workbook.xml.rels",
    "xl/styles.xml",
    "xl/vbaProject.bin",
    "xl/workbook.xml",
    "xl/worksheets/sheet1.xml",
  ]);
  const types = zip.get("[Content_Types].xml").toString("utf8");
  assert.match(types, new RegExp(`<Override PartName="/xl/workbook.xml" ContentType="${ADDIN_CONTENT_TYPE.replace(/[.+]/g, "\\$&")}"/>`));
  assert.match(types, /<Default Extension="bin" ContentType="application\/vnd.ms-office.vbaProject"\/>/);
  const rels = zip.get("xl/_rels/workbook.xml.rels").toString("utf8");
  assert.ok(rels.includes(`Type="${VBA_REL_TYPE}" Target="vbaProject.bin"`));
  assert.match(zip.get("_rels/.rels").toString("utf8"), /officeDocument" Target="xl\/workbook.xml"/);
  // Every part that is referenced exists, and every XML part has a content type.
  for (const m of rels.matchAll(/Target="([^"]+)"/g)) assert.ok(zip.has("xl/" + m[1]), m[1]);
  for (const name of zip.keys()) {
    if (name.endsWith(".rels") || name === "[Content_Types].xml" || name.endsWith(".bin")) continue;
    assert.ok(types.includes(`PartName="/${name}"`), `${name} has no content type`);
  }
  // Document module names are the codeName attributes of the workbook and the sheet.
  assert.match(zip.get("xl/workbook.xml").toString("utf8"), /<workbookPr codeName="ThisWorkbook"\/>/);
  assert.match(zip.get("xl/worksheets/sheet1.xml").toString("utf8"), /<sheetPr codeName="Sheet1"\/>/);
  // Well-formed enough: XML declaration, one root, balanced tags.
  for (const [name, data] of zip) {
    if (name.endsWith(".bin")) continue;
    const xml = data.toString("utf8").replace(/^<\?xml[^>]*\?>\r?\n/, "");
    const stack = [];
    for (const [, close, tag, self] of xml.matchAll(/<(\/?)([A-Za-z_:][\w:.-]*)[^>]*?(\/?)>/g)) {
      if (self) continue;
      if (close) assert.equal(stack.pop(), tag, `${name}: </${tag}>`);
      else stack.push(tag);
    }
    assert.equal(stack.length, 0, `${name}: unclosed ${stack}`);
  }
});

test("xlam: compound file holds the VBA project storage of [MS-OVBA] 2.2", () => {
  assert.equal(cfb.major, 3);
  assert.deepEqual([...cfb.streams.keys()].sort(), [
    "PROJECT",
    "PROJECTwm",
    "VBA/Sheet1",
    "VBA/TafqitLib",
    "VBA/ThisWorkbook",
    "VBA/_VBA_PROJECT",
    "VBA/dir",
  ]);
  // Source-only project: no performance cache, Version 0xFFFF, so Office compiles the source.
  assert.equal(cfb.streams.get("VBA/_VBA_PROJECT").toString("hex"), "cc61ffff000000");
});

test("xlam: TafqitLib module stream decompresses to vba/Tafqit.bas, byte for byte", () => {
  const m = dir.modules.find((x) => x.name === "TafqitLib");
  assert.ok(m, "TafqitLib in dir");
  assert.equal(m.type, "procedural");
  const stream = cfb.streams.get("VBA/" + m.streamName.mbcs);
  const source = ovbaDecompress(stream.subarray(m.offset));
  assert.equal(source.length, bas.length);
  assert.ok(source.equals(bas), "module source differs from vba/Tafqit.bas");
});

test("xlam: document modules carry their standard attributes", () => {
  for (const [name, clsid] of [
    ["ThisWorkbook", "00020819-0000-0000-C000-000000000046"],
    ["Sheet1", "00020820-0000-0000-C000-000000000046"],
  ]) {
    const m = dir.modules.find((x) => x.name === name);
    assert.equal(m.type, "document");
    const src = ovbaDecompress(cfb.streams.get("VBA/" + m.streamName.mbcs).subarray(m.offset)).toString("latin1");
    assert.ok(src.startsWith(`Attribute VB_Name = "${name}"\r\nAttribute VB_Base = "0{${clsid}}"\r\n`), src);
    assert.ok(src.includes("Attribute VB_PredeclaredId = True\r\n") && src.endsWith("\r\n"));
  }
});

test("xlam: dir stream agrees with PROJECT and PROJECTwm", () => {
  assert.equal(dir.name, PROJECT_NAME);
  assert.equal(dir.codePage, 1252);
  assert.equal(dir.lcid, 0x409);
  assert.equal(dir.lcidInvoke, 0x409);
  assert.equal(dir.cookie, 0xffff);
  assert.deepEqual(
    dir.references.map((r) => [r.name.mbcs, r.name.unicode, r.libid.split("#")[0]]),
    [["stdole", "stdole", "*\\G{00020430-0000-0000-C000-000000000046}"]],
  );
  for (const m of dir.modules) {
    assert.equal(m.nameUnicode, m.name);
    assert.equal(m.streamName.mbcs, m.name);
    assert.equal(m.streamName.unicode, m.name);
    assert.equal(m.offset, 0);
    assert.equal(m.cookie, 0xffff);
  }

  assert.ok(project.endsWith("\r\n") && !/[^\r]\n/.test(project));
  const lines = project.split("\r\n");
  assert.match(lines[0], /^ID="\{[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\}"$/);
  assert.ok(lines.includes(`Name="${dir.name}"`));
  const declared = lines.filter((l) => /^(Document|Module)=/.test(l));
  assert.deepEqual(
    declared,
    dir.modules.map((m) => (m.type === "document" ? `Document=${m.name}/&H00000000` : `Module=${m.name}`)),
  );
  assert.ok(lines.includes("[Host Extender Info]"));
  assert.ok(lines.includes("&H00000001={3832D640-CF90-11CF-8E43-00A0C911005A};VBE;&H00000000"));

  const wm = cfb.streams.get("PROJECTwm");
  const expected = Buffer.concat([
    ...dir.modules.map((m) => Buffer.concat([Buffer.from(m.name + "\0", "latin1"), Buffer.from(m.name + "\0", "utf16le")])),
    Buffer.alloc(2),
  ]);
  assert.ok(wm.equals(expected));
});

test("xlam: project is unlocked, without password and visible (CMG / DPB / GC)", () => {
  const id = /^ID="([^"]+)"/.exec(project)[1];
  let projKey = 0;
  for (const b of Buffer.from(id, "latin1")) projKey = (projKey + b) & 0xff;
  const get = (k) => ovbaDecrypt(new RegExp(`^${k}="([0-9A-F]+)"\\r$`, "m").exec(project)[1]);
  for (const [k, data] of [["CMG", "00000000"], ["DPB", "00"], ["GC", "ff"]]) {
    const d = get(k);
    assert.equal(d.version, 2, k);
    assert.equal(d.projKey, projKey, k);
    assert.equal(d.data.toString("hex"), data, k);
  }
});

test("decrypt: values written by Excel decode as expected (reader cross-check)", () => {
  // From a VBA project saved by Excel: GC visible, DPB no password, CMG unprotected.
  assert.equal(ovbaDecrypt("1517CAF1D6F9D7F9D706").data.toString("hex"), "ff");
  assert.equal(ovbaDecrypt("0E0CD1ECDFF4E7F5E7F5E7").data.toString("hex"), "00");
  assert.equal(ovbaDecrypt("0705D8E3D8EDDBF1DBF1DBF1DBF1").data.toString("hex"), "00000000");
});

test("compression: [MS-OVBA] 3.2 examples decompress as the spec says", () => {
  const h = (s) => Buffer.from(s.replace(/ /g, ""), "hex");
  assert.equal(ovbaDecompress(h("01 19 B0 00 61 62 63 64 65 66 67 68 00 69 6A 6B 6C 6D 6E 6F 70 00 71 72 73 74 75 76 2E")).toString(), "abcdefghijklmnopqrstuv.");
  assert.equal(
    ovbaDecompress(h("01 2F B0 00 23 61 61 61 62 63 64 65 82 66 00 70 61 67 68 69 6A 01 38 08 61 6B 6C 00 30 6D 6E 6F 70 06 71 02 70 04 10 72 73 74 75 76 10 77 78 79 7A 00 3C")).toString(),
    "#aaabcdefaaaaghijaaaaaklaaamnopqaaaaaaaaaaaarstuvwxyzaaa",
  );
  // The writer gives the spec's own bytes for the literal-only example.
  assert.equal(ovbaCompress(Buffer.from("abcdefghijklmnopqrstuv.")).toString("hex").toUpperCase(), "0119B000616263646566676800696A6B6C6D6E6F70007172737475762E");
});

test("compression: round trip on edge cases and random data", () => {
  let seed = 12345;
  const rand = () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x80000000;
  const randomBytes = (n, alphabet = 256) => Buffer.from(Array.from({ length: n }, () => Math.floor(rand() * alphabet)));
  const cases = [
    Buffer.alloc(0),
    Buffer.from("a"),
    Buffer.from("ab"),
    Buffer.from("aaa"),
    Buffer.alloc(4095, 0x61),
    Buffer.alloc(4096, 0x61),
    Buffer.alloc(4097, 0x61),
    Buffer.alloc(20000, 0x20),
    randomBytes(4096), // incompressible full chunk: raw chunk
    Buffer.concat([randomBytes(8192), Buffer.from("tail text tail text")]),
    randomBytes(10000, 4),
    randomBytes(12289, 16),
    bas.subarray(0, 4096),
    bas.subarray(1, 9000),
  ];
  for (const [i, input] of cases.entries()) {
    const c = ovbaCompress(input);
    assert.ok(ovbaDecompress(c).equals(input), `case ${i} (${input.length} bytes)`);
  }
  assert.ok(ovbaCompress(bas).length < bas.length / 2, "the module compresses");
});

test("olevba (oletools) extracts the same module source", { skip: !process.env.OLETOOLS_PYTHON && "set OLETOOLS_PYTHON to a Python with oletools" }, () => {
  const tmp = mkdtempSync(join(tmpdir(), "xlam-"));
  try {
    const file = join(tmp, "InstaWords.xlam");
    writeFileSync(file, xlam);
    const out = execFileSync(process.env.OLETOOLS_PYTHON, ["-I", fileURLToPath(new URL("./olevba_check.py", import.meta.url)), file, vbaDir + "Tafqit.bas"], { encoding: "utf8" });
    assert.match(out, /^olevba: OK/m);
  } finally {
    rmSync(tmp, { recursive: true, force: true });
  }
});
