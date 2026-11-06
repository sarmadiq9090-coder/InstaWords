// Build the Excel add-in InstaWords.xlam from vba/Tafqit.bas (run via `npm run build:xlam`).
//
//   node scripts/build-xlam.mjs [--bas vba/Tafqit.bas] [--out out/InstaWords.xlam]
//
// No dependencies, byte-for-byte reproducible (fixed GUID, seeds and zip timestamps; zip entries
// are stored, so the output does not depend on the zlib version). Written to the specs:
//   - [MS-OVBA] the VBA project: PROJECT, PROJECTwm, VBA/dir, VBA/_VBA_PROJECT and one stream per
//     module, every module stream holding only its MS-OVBA-compressed source (MODULEOFFSET 0) and
//     _VBA_PROJECT being the 7-byte form with Version 0xFFFF, so Office compiles from the source
//     (no p-code / performance cache, as [MS-OVBA] 2.3.4.1 says to write).
//   - [MS-CFB] version 3 compound file (512-byte sectors, 64-byte mini stream under 4096 bytes).
//   - [ECMA-376] / [MS-XLSX] package: workbook part of type
//     application/vnd.ms-excel.addin.macroEnabled.main+xml with a vbaProject relationship.
// Modules: ThisWorkbook and Sheet1 (document modules matching the codeName attributes in the
// workbook and sheet parts, as Excel writes them) and TafqitLib = vba/Tafqit.bas verbatim.
//
// Verified by tests/vba/xlam.test.mjs (an independent reader round-trips every stream) and in CI
// by oletools/olevba (tests/vba/olevba_check.py). It has not been opened by Excel in CI: see
// docs/TESTING-CHECKLIST.md, section 13.
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { crc32 } from "node:zlib";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");

/* ------------------------------------------------------------------ bytes */

class Bytes {
  constructor() {
    this.parts = [];
  }
  u8(v) {
    this.parts.push(Buffer.of(v & 0xff));
    return this;
  }
  u16(v) {
    const b = Buffer.alloc(2);
    b.writeUInt16LE(v);
    this.parts.push(b);
    return this;
  }
  u32(v) {
    const b = Buffer.alloc(4);
    b.writeUInt32LE(v >>> 0);
    this.parts.push(b);
    return this;
  }
  bytes(b) {
    this.parts.push(Buffer.from(b));
    return this;
  }
  get length() {
    return this.parts.reduce((n, p) => n + p.length, 0);
  }
  done() {
    return Buffer.concat(this.parts);
  }
}

/** MBCS text of the project (code page 1252). Only ASCII is used, so it is checked and kept as is. */
function ansi(s) {
  if (!/^[\x20-\x7e]*$/.test(s)) throw new Error(`build-xlam: non-ASCII project text: ${JSON.stringify(s)}`);
  return Buffer.from(s, "latin1");
}
const utf16 = (s) => Buffer.from(s, "utf16le");

/** Module source in the project code page: printable ASCII, tabs and CRLF only (like vba/Tafqit.bas). */
function sourceBytes(m) {
  if (!/^[\x20-\x7e\t\r\n]*$/.test(m.source) || /\r(?!\n)|(?<!\r)\n/.test(m.source)) {
    throw new Error(`build-xlam: module ${m.name}: source must be ASCII with CRLF line ends`);
  }
  return Buffer.from(m.source, "latin1");
}

/* ------------------------------------------- [MS-OVBA] 2.4.1 compression */

const CHUNK = 4096;

/**
 * Compresses `input` into a CompressedContainer ([MS-OVBA] 2.4.1.1.1): signature byte 0x01, then one
 * CompressedChunk per 4096 bytes of input. Matches are searched only inside the current chunk, with
 * the copy-token layout of 2.4.1.3.19.1 (offset bits grow with the position in the chunk).
 */
export function ovbaCompress(input) {
  const src = Buffer.from(input);
  const parts = [Buffer.of(0x01)];
  for (let start = 0; start < src.length; start += CHUNK) {
    parts.push(compressChunk(src, start, Math.min(start + CHUNK, src.length)));
  }
  return Buffer.concat(parts);
}

function compressChunk(src, start, end) {
  const data = [];
  // Hash chains of 3-byte prefixes, local to this chunk.
  const head = new Int32Array(1 << 16).fill(-1);
  const prev = new Int32Array(CHUNK).fill(-1);
  const hash = (p) => ((src[p] << 8) ^ (src[p + 1] << 4) ^ src[p + 2]) & 0xffff;
  const insert = (p) => {
    if (p + 2 >= end) return;
    const h = hash(p);
    prev[p - start] = head[h];
    head[h] = p;
  };

  let pos = start;
  while (pos < end) {
    const flagAt = data.length;
    data.push(0);
    let flags = 0;
    for (let bit = 0; bit < 8 && pos < end; bit++) {
      const diff = pos - start;
      let bestLen = 0;
      let bestOff = 0;
      let bitCount = 4;
      if (diff > 0 && pos + 2 < end) {
        while (1 << bitCount < diff) bitCount++;
        const maxLen = Math.min((0xffff >> bitCount) + 3, end - pos);
        let cand = head[hash(pos)];
        for (let tries = 0; cand !== -1 && tries < 512; tries++, cand = prev[cand - start]) {
          let len = 0;
          while (len < maxLen && src[cand + len] === src[pos + len]) len++;
          if (len > bestLen) {
            bestLen = len;
            bestOff = pos - cand;
            if (len === maxLen) break;
          }
        }
      }
      if (bestLen >= 3) {
        const token = ((bestOff - 1) << (16 - bitCount)) | (bestLen - 3);
        data.push(token & 0xff, token >>> 8);
        flags |= 1 << bit;
        for (let i = 0; i < bestLen; i++) insert(pos + i);
        pos += bestLen;
      } else {
        data.push(src[pos]);
        insert(pos);
        pos++;
      }
    }
    data[flagAt] = flags;
  }

  if (data.length <= CHUNK) {
    // CompressedChunkHeader: size - 3 in bits 0-11, signature 0b011, flag 1 (compressed).
    const header = 0xb000 | (data.length + 2 - 3);
    return Buffer.concat([Buffer.of(header & 0xff, header >>> 8), Buffer.from(data)]);
  }
  // Incompressible: a raw chunk always decompresses to exactly 4096 bytes (2.4.1.3.10), which a
  // short final chunk cannot be without padding the source. Text never gets here.
  if (end - start !== CHUNK) throw new Error("build-xlam: incompressible final chunk");
  return Buffer.concat([Buffer.of(0xff, 0x3f), src.subarray(start, end)]);
}

/* --------------------------------- [MS-OVBA] 2.4.3 PROJECT data encryption */

/** Encrypts `data` for the CMG / DPB / GC properties ([MS-OVBA] 2.4.3.2); returns upper-case hex. */
export function ovbaEncrypt(data, projectId, seed) {
  let projKey = 0;
  for (const b of ansi(projectId)) projKey = (projKey + b) & 0xff;
  const version = 2;
  const out = [seed, seed ^ version, seed ^ projKey];
  let unencrypted1 = projKey;
  let encrypted1 = seed ^ projKey;
  let encrypted2 = seed ^ version;
  const push = (byte) => {
    const enc = byte ^ ((encrypted2 + unencrypted1) & 0xff);
    out.push(enc);
    encrypted2 = encrypted1;
    encrypted1 = enc;
    unencrypted1 = byte;
  };
  const ignoredLength = (seed & 6) / 2;
  for (let i = 0; i < ignoredLength; i++) push(0);
  const len = data.length;
  for (const b of [len & 0xff, (len >>> 8) & 0xff, (len >>> 16) & 0xff, (len >>> 24) & 0xff]) push(b);
  for (const b of data) push(b);
  return Buffer.from(out).toString("hex").toUpperCase();
}

/* ------------------------------------------------ [MS-OVBA] VBA project */

const DOCUMENT_ATTRIBUTES = (name, baseClsid) =>
  [
    `Attribute VB_Name = "${name}"`,
    `Attribute VB_Base = "0{${baseClsid}}"`,
    "Attribute VB_GlobalNameSpace = False",
    "Attribute VB_Creatable = False",
    "Attribute VB_PredeclaredId = True",
    "Attribute VB_Exposed = True",
    "Attribute VB_TemplateDerived = False",
    "Attribute VB_Customizable = True",
    "",
  ].join("\r\n");

/** Fixed, so the build is reproducible. Any GUID is valid for a new project. */
export const PROJECT_ID = "{3E9A5B71-2C4D-4F8E-A1B6-7D0C9E2F4A58}";
export const PROJECT_NAME = "InstaWords";
const STDOLE_LIBID = "*\\G{00020430-0000-0000-C000-000000000046}#2.0#0#C:\\Windows\\System32\\stdole2.tlb#OLE Automation";

/**
 * The modules of the add-in: the two document modules Excel keeps for a one-sheet workbook
 * (Workbook coclass 00020819, Worksheet coclass 00020820) and the TAFQIT module.
 */
export function addinModules(basText) {
  const name = /^Attribute VB_Name = "([A-Za-z][A-Za-z0-9_]*)"\r\n/.exec(basText)?.[1];
  if (!name) throw new Error('build-xlam: the .bas must start with Attribute VB_Name = "..." and CRLF');
  return [
    { name: "ThisWorkbook", type: "document", source: DOCUMENT_ATTRIBUTES("ThisWorkbook", "00020819-0000-0000-C000-000000000046") },
    { name: "Sheet1", type: "document", source: DOCUMENT_ATTRIBUTES("Sheet1", "00020820-0000-0000-C000-000000000046") },
    { name, type: "procedural", source: basText },
  ];
}

/** The dir stream before compression ([MS-OVBA] 2.3.4.2). */
export function dirStream(modules, { projectName = PROJECT_NAME, codePage = 1252 } = {}) {
  const w = new Bytes();
  const rec = (id, payload) => w.u16(id).u32(payload.length).bytes(payload);
  const u16 = (v) => new Bytes().u16(v).done();
  const u32 = (v) => new Bytes().u32(v).done();
  const pair = (id, text, reservedId, unicode) =>
    w.u16(id).u32(text.length).bytes(text).u16(reservedId).u32(unicode.length).bytes(unicode);

  // PROJECTINFORMATION
  rec(0x0001, u32(0x00000001)); // PROJECTSYSKIND: 32-bit Windows (recompiled on any platform)
  rec(0x0002, u32(0x0409)); // PROJECTLCID
  rec(0x0014, u32(0x0409)); // PROJECTLCIDINVOKE
  rec(0x0003, u16(codePage)); // PROJECTCODEPAGE
  rec(0x0004, ansi(projectName)); // PROJECTNAME
  pair(0x0005, ansi(""), 0x0040, utf16("")); // PROJECTDOCSTRING
  pair(0x0006, ansi(""), 0x003d, ansi("")); // PROJECTHELPFILEPATH (HelpFile2 = HelpFile1)
  rec(0x0007, u32(0)); // PROJECTHELPCONTEXT
  rec(0x0008, u32(0)); // PROJECTLIBFLAGS
  w.u16(0x0009).u32(0x00000004).u32(1).u16(0); // PROJECTVERSION: Reserved 4, VersionMajor, VersionMinor
  pair(0x000c, ansi(""), 0x003c, utf16("")); // PROJECTCONSTANTS

  // PROJECTREFERENCES: OLE Automation, which Excel adds to every new project.
  pair(0x0016, ansi("stdole"), 0x003e, utf16("stdole")); // REFERENCENAME
  const libid = ansi(STDOLE_LIBID);
  w.u16(0x000d).u32(4 + libid.length + 4 + 2).u32(libid.length).bytes(libid).u32(0).u16(0); // REFERENCEREGISTERED

  // PROJECTMODULES
  w.u16(0x000f).u32(2).u16(modules.length);
  rec(0x0013, u16(0xffff)); // PROJECTCOOKIE
  for (const m of modules) {
    rec(0x0019, ansi(m.name)); // MODULENAME
    rec(0x0047, utf16(m.name)); // MODULENAMEUNICODE
    pair(0x001a, ansi(m.name), 0x0032, utf16(m.name)); // MODULESTREAMNAME
    pair(0x001c, ansi(""), 0x0048, utf16("")); // MODULEDOCSTRING
    rec(0x0031, u32(0)); // MODULEOFFSET: the source starts at 0 (no performance cache)
    rec(0x001e, u32(0)); // MODULEHELPCONTEXT
    rec(0x002c, u16(0xffff)); // MODULECOOKIE
    w.u16(m.type === "document" ? 0x0022 : 0x0021).u32(0); // MODULETYPE
    w.u16(0x002b).u32(0); // Terminator
  }
  w.u16(0x0010).u32(0); // dir Terminator
  return w.done();
}

/** The PROJECT stream ([MS-OVBA] 2.3.1): text, CRLF lines, code page of the project. */
export function projectStream(modules, { projectId = PROJECT_ID, projectName = PROJECT_NAME } = {}) {
  const lines = [`ID="${projectId}"`];
  for (const m of modules) lines.push(m.type === "document" ? `Document=${m.name}/&H00000000` : `Module=${m.name}`);
  lines.push(
    `Name="${projectName}"`,
    'HelpContextID="0"',
    'VersionCompatible32="393222000"',
    // Not protected (4 bytes 0), no password (1 byte 0), visible (1 byte 0xFF): 2.3.1.15-17.
    `CMG="${ovbaEncrypt(Buffer.alloc(4), projectId, 0x07)}"`,
    `DPB="${ovbaEncrypt(Buffer.of(0x00), projectId, 0x0e)}"`,
    `GC="${ovbaEncrypt(Buffer.of(0xff), projectId, 0x15)}"`,
    "",
    "[Host Extender Info]",
    "&H00000001={3832D640-CF90-11CF-8E43-00A0C911005A};VBE;&H00000000",
    "",
  );
  for (const l of lines) ansi(l); // ASCII check
  return Buffer.from(lines.join("\r\n"), "latin1"); // ends with CRLF: the last line is empty
}

/** PROJECTwm ([MS-OVBA] 2.3.3): MBCS and UTF-16 name of each module, then 0x0000. */
export function projectWmStream(modules) {
  const w = new Bytes();
  for (const m of modules) w.bytes(ansi(m.name)).u8(0).bytes(utf16(m.name)).u16(0);
  return w.u16(0).done();
}

/** xl/vbaProject.bin: a compound file with the VBA project storage of [MS-OVBA] 2.2. */
export function buildVbaProjectBin(modules) {
  const vba = [
    // _VBA_PROJECT ([MS-OVBA] 2.3.4.1): Reserved1 0x61CC, Version 0xFFFF, Reserved2 0, Reserved3; no cache.
    { name: "_VBA_PROJECT", data: Buffer.of(0xcc, 0x61, 0xff, 0xff, 0x00, 0x00, 0x00) },
    { name: "dir", data: ovbaCompress(dirStream(modules)) },
    ...modules.map((m) => ({ name: m.name, data: ovbaCompress(sourceBytes(m)) })),
  ];
  return writeCfb({
    name: "Root Entry",
    children: [
      { name: "PROJECT", data: projectStream(modules) },
      { name: "PROJECTwm", data: projectWmStream(modules) },
      { name: "VBA", children: vba },
    ],
  });
}

/* --------------------------------------------- [MS-CFB] compound file v3 */

const SECTOR = 512;
const MINI = 64;
const CUTOFF = 4096;
const FREESECT = 0xffffffff;
const ENDOFCHAIN = 0xfffffffe;
const FATSECT = 0xfffffffd;
const NOSTREAM = 0xffffffff;

/** [MS-CFB] 2.6.4: shorter names first, then code point order of the upper-cased names. */
export function cfbCompare(a, b) {
  if (a.length !== b.length) return a.length - b.length;
  const A = a.toUpperCase();
  const B = b.toUpperCase();
  for (let i = 0; i < A.length; i++) if (A.charCodeAt(i) !== B.charCodeAt(i)) return A.charCodeAt(i) - B.charCodeAt(i);
  return 0;
}

/**
 * Writes a version 3 compound file. `tree` is the root storage: { name, children: [node] }, where a
 * node is a storage { name, children } or a stream { name, data }.
 */
export function writeCfb(tree) {
  // Directory entries, root first; each storage's children form a red-black tree.
  const entries = [];
  const add = (node, type) => {
    const e = { node, type, left: NOSTREAM, right: NOSTREAM, child: NOSTREAM, color: 1, start: 0, size: 0 };
    if (node.name.length > 31) throw new Error(`build-xlam: CFB name too long: ${node.name}`);
    entries.push(e);
    return e;
  };
  const visit = (storageEntry) => {
    const kids = [...storageEntry.node.children].sort((a, b) => cfbCompare(a.name, b.name));
    for (let i = 1; i < kids.length; i++) if (cfbCompare(kids[i - 1].name, kids[i].name) === 0) throw new Error(`build-xlam: duplicate name ${kids[i].name}`);
    const ids = kids.map((k) => {
      add(k, k.children ? 1 : 2);
      return entries.length - 1;
    });
    // Balanced binary search tree over the sorted siblings. Nodes on the deepest level are red when
    // that level is not full, all others black: every path then has the same number of black nodes.
    const depth = [];
    const build = (lo, hi, d) => {
      if (lo > hi) return NOSTREAM;
      const mid = (lo + hi) >>> 1;
      const e = entries[ids[mid]];
      depth[mid] = d;
      e.left = build(lo, mid - 1, d + 1);
      e.right = build(mid + 1, hi, d + 1);
      return ids[mid];
    };
    storageEntry.child = build(0, ids.length - 1, 0);
    const maxDepth = Math.max(-1, ...depth);
    const full = ids.length === 2 ** (maxDepth + 1) - 1;
    if (maxDepth > 0 && !full) depth.forEach((d, i) => d === maxDepth && (entries[ids[i]].color = 0));
    for (const id of ids) if (entries[id].type === 1) visit(entries[id]);
  };
  const rootEntry = add(tree, 5);
  visit(rootEntry);

  // Mini stream (streams under 4096 bytes) and regular streams.
  const streams = entries.filter((e) => e.type === 2);
  const miniParts = [];
  const miniFat = [];
  const big = [];
  for (const e of streams) {
    const data = e.node.data;
    e.size = data.length;
    if (data.length === 0) {
      e.start = ENDOFCHAIN;
    } else if (data.length < CUTOFF) {
      const n = Math.ceil(data.length / MINI);
      e.start = miniFat.length;
      for (let i = 0; i < n; i++) miniFat.push(i === n - 1 ? ENDOFCHAIN : miniFat.length + 1);
      miniParts.push(data, Buffer.alloc(n * MINI - data.length));
    } else {
      big.push(e);
    }
  }
  const miniStream = Buffer.concat(miniParts);

  const sectors = (bytes) => Math.ceil(bytes / SECTOR);
  const dirSectors = sectors(entries.length * 128);
  const miniFatSectors = sectors(miniFat.length * 4);
  const miniStreamSectors = sectors(miniStream.length);
  const bigSectors = big.map((e) => sectors(e.size));
  const nonFat = dirSectors + miniFatSectors + miniStreamSectors + bigSectors.reduce((a, b) => a + b, 0);
  let fatSectors = 1;
  while (fatSectors * (SECTOR / 4) < nonFat + fatSectors) fatSectors++;
  if (fatSectors > 109) throw new Error("build-xlam: file too large for a DIFAT-less compound file");

  const fat = new Array(fatSectors * (SECTOR / 4)).fill(FREESECT);
  let next = 0;
  const chain = (count) => {
    if (count === 0) return ENDOFCHAIN;
    const first = next;
    for (let i = 0; i < count; i++) fat[next + i] = i === count - 1 ? ENDOFCHAIN : next + i + 1;
    next += count;
    return first;
  };
  for (let i = 0; i < fatSectors; i++) fat[next++] = FATSECT;
  const firstDir = chain(dirSectors);
  const firstMiniFat = chain(miniFatSectors);
  rootEntry.start = chain(miniStreamSectors);
  rootEntry.size = miniStream.length;
  big.forEach((e, i) => (e.start = chain(bigSectors[i])));

  // Header ([MS-CFB] 2.2).
  const header = Buffer.alloc(SECTOR);
  Buffer.from("D0CF11E0A1B11AE1", "hex").copy(header, 0);
  header.writeUInt16LE(0x003e, 24); // minor version
  header.writeUInt16LE(0x0003, 26); // major version 3
  header.writeUInt16LE(0xfffe, 28); // byte order
  header.writeUInt16LE(0x0009, 30); // sector shift: 512
  header.writeUInt16LE(0x0006, 32); // mini sector shift: 64
  header.writeUInt32LE(0, 40); // number of directory sectors (0 for version 3)
  header.writeUInt32LE(fatSectors, 44);
  header.writeUInt32LE(firstDir, 48);
  header.writeUInt32LE(0, 52); // transaction signature
  header.writeUInt32LE(CUTOFF, 56);
  header.writeUInt32LE(miniFatSectors ? firstMiniFat : ENDOFCHAIN, 60);
  header.writeUInt32LE(miniFatSectors, 64);
  header.writeUInt32LE(ENDOFCHAIN, 68); // first DIFAT sector
  header.writeUInt32LE(0, 72); // number of DIFAT sectors
  for (let i = 0; i < 109; i++) header.writeUInt32LE(i < fatSectors ? i : FREESECT, 76 + i * 4);

  const pad = (buf) => Buffer.concat([buf, Buffer.alloc(sectors(buf.length) * SECTOR - buf.length)]);
  const fatBuf = Buffer.alloc(fat.length * 4);
  fat.forEach((v, i) => fatBuf.writeUInt32LE(v, i * 4));

  // Directory ([MS-CFB] 2.6); free entries are zero with NOSTREAM links.
  const dir = Buffer.alloc(dirSectors * SECTOR);
  for (let i = 0; i < dirSectors * 4; i++) {
    const o = i * 128;
    const e = entries[i];
    if (!e) {
      dir.writeUInt32LE(NOSTREAM, o + 68);
      dir.writeUInt32LE(NOSTREAM, o + 72);
      dir.writeUInt32LE(NOSTREAM, o + 76);
      continue;
    }
    const name = utf16(e.node.name);
    name.copy(dir, o);
    dir.writeUInt16LE(name.length + 2, o + 64);
    dir[o + 66] = e.type;
    dir[o + 67] = e.color;
    dir.writeUInt32LE(e.left, o + 68);
    dir.writeUInt32LE(e.right, o + 72);
    dir.writeUInt32LE(e.child, o + 76);
    // CLSID, state bits and timestamps stay zero; storages have start sector 0 and size 0.
    dir.writeUInt32LE(e.type === 1 ? 0 : e.start, o + 116);
    dir.writeUInt32LE(e.type === 1 ? 0 : e.size, o + 120);
  }

  const miniFatBuf = Buffer.alloc(miniFatSectors * SECTOR, 0xff);
  miniFat.forEach((v, i) => miniFatBuf.writeUInt32LE(v, i * 4));

  return Buffer.concat([header, fatBuf, dir, miniFatBuf, pad(miniStream), ...big.map((e) => pad(e.node.data))]);
}

/* ------------------------------------------------------- zip (stored) */

const DOS_DATE = 0x0021; // 1980-01-01
const DOS_TIME = 0;

/** A zip archive with stored (uncompressed) entries, in the given order. */
export function zipStore(files) {
  const local = [];
  const central = [];
  let offset = 0;
  for (const { name, data } of files) {
    const n = Buffer.from(name, "utf8");
    const crc = crc32(data);
    const lh = Buffer.alloc(30);
    lh.writeUInt32LE(0x04034b50, 0);
    lh.writeUInt16LE(20, 4); // version needed
    lh.writeUInt16LE(0, 6); // flags
    lh.writeUInt16LE(0, 8); // method: stored
    lh.writeUInt16LE(DOS_TIME, 10);
    lh.writeUInt16LE(DOS_DATE, 12);
    lh.writeUInt32LE(crc, 14);
    lh.writeUInt32LE(data.length, 18);
    lh.writeUInt32LE(data.length, 22);
    lh.writeUInt16LE(n.length, 26);
    lh.writeUInt16LE(0, 28);
    const ch = Buffer.alloc(46);
    ch.writeUInt32LE(0x02014b50, 0);
    ch.writeUInt16LE(20, 4); // version made by (MS-DOS, 2.0)
    ch.writeUInt16LE(20, 6);
    ch.writeUInt16LE(0, 8);
    ch.writeUInt16LE(0, 10);
    ch.writeUInt16LE(DOS_TIME, 12);
    ch.writeUInt16LE(DOS_DATE, 14);
    ch.writeUInt32LE(crc, 16);
    ch.writeUInt32LE(data.length, 20);
    ch.writeUInt32LE(data.length, 24);
    ch.writeUInt16LE(n.length, 28);
    ch.writeUInt32LE(offset, 42);
    local.push(lh, n, data);
    central.push(ch, n);
    offset += lh.length + n.length + data.length;
  }
  const cd = Buffer.concat(central);
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);
  end.writeUInt16LE(files.length, 8);
  end.writeUInt16LE(files.length, 10);
  end.writeUInt32LE(cd.length, 12);
  end.writeUInt32LE(offset, 16);
  return Buffer.concat([...local, cd, end]);
}

/* ------------------------------------------------------- OOXML package */

const XML = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\r\n';
const NS_MAIN = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
const NS_R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
const NS_PKG_REL = "http://schemas.openxmlformats.org/package/2006/relationships";
export const ADDIN_CONTENT_TYPE = "application/vnd.ms-excel.addin.macroEnabled.main+xml";
export const VBA_REL_TYPE = "http://schemas.microsoft.com/office/2006/relationships/vbaProject";

const esc = (s) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

/** The InstaWords.xlam package, from the text of vba/Tafqit.bas. */
export function buildXlam(basText, { version = "" } = {}) {
  const modules = addinModules(basText);
  const vbaProject = buildVbaProjectBin(modules);
  const title = "InstaWords — تفقيط (TAFQIT)";
  const description =
    `=TAFQIT(A1, "SAR"): المبلغ بالحروف بالعربية والإنجليزية والفرنسية، دون إنترنت. ` +
    `Amount in words in Arabic, English and French, offline.${version ? ` InstaWords ${version}.` : ""} https://fastkiwi.app/instawords/excel.html`;
  const parts = [
    [
      "[Content_Types].xml",
      XML +
        '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">' +
        '<Default Extension="bin" ContentType="application/vnd.ms-office.vbaProject"/>' +
        '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>' +
        '<Default Extension="xml" ContentType="application/xml"/>' +
        `<Override PartName="/xl/workbook.xml" ContentType="${ADDIN_CONTENT_TYPE}"/>` +
        '<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>' +
        '<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>' +
        '<Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>' +
        "</Types>",
    ],
    [
      "_rels/.rels",
      XML +
        `<Relationships xmlns="${NS_PKG_REL}">` +
        '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>' +
        '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>' +
        "</Relationships>",
    ],
    [
      "docProps/core.xml",
      XML +
        '<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/">' +
        `<dc:title>${esc(title)}</dc:title><dc:description>${esc(description)}</dc:description><dc:creator>FastKiwi</dc:creator>` +
        "</cp:coreProperties>",
    ],
    [
      "xl/workbook.xml",
      XML +
        `<workbook xmlns="${NS_MAIN}" xmlns:r="${NS_R}">` +
        '<workbookPr codeName="ThisWorkbook"/>' +
        "<bookViews><workbookView/></bookViews>" +
        '<sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets>' +
        "</workbook>",
    ],
    [
      "xl/_rels/workbook.xml.rels",
      XML +
        `<Relationships xmlns="${NS_PKG_REL}">` +
        '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>' +
        '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>' +
        `<Relationship Id="rId3" Type="${VBA_REL_TYPE}" Target="vbaProject.bin"/>` +
        "</Relationships>",
    ],
    [
      "xl/worksheets/sheet1.xml",
      XML +
        `<worksheet xmlns="${NS_MAIN}" xmlns:r="${NS_R}">` +
        '<sheetPr codeName="Sheet1"/><dimension ref="A1"/>' +
        '<sheetViews><sheetView workbookViewId="0"/></sheetViews>' +
        '<sheetFormatPr defaultRowHeight="15"/><sheetData/>' +
        '<pageMargins left="0.7" right="0.7" top="0.75" bottom="0.75" header="0.3" footer="0.3"/>' +
        "</worksheet>",
    ],
    [
      "xl/styles.xml",
      XML +
        `<styleSheet xmlns="${NS_MAIN}">` +
        '<fonts count="1"><font><sz val="11"/><name val="Calibri"/><family val="2"/></font></fonts>' +
        '<fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>' +
        '<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>' +
        '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>' +
        '<cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs>' +
        '<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>' +
        "</styleSheet>",
    ],
    ["xl/vbaProject.bin", vbaProject],
  ];
  return zipStore(parts.map(([name, data]) => ({ name, data: Buffer.isBuffer(data) ? data : Buffer.from(data, "utf8") })));
}

/* ------------------------------------------------------------------ CLI */

function main(argv) {
  const opt = (name, def) => {
    const i = argv.indexOf(name);
    return i === -1 ? def : resolve(argv[i + 1]);
  };
  const basPath = opt("--bas", resolve(root, "vba/Tafqit.bas"));
  const outPath = opt("--out", resolve(root, "out/InstaWords.xlam"));
  const { version } = JSON.parse(readFileSync(resolve(root, "package.json"), "utf8"));
  const xlam = buildXlam(readFileSync(basPath, "latin1"), { version });
  mkdirSync(dirname(outPath), { recursive: true });
  writeFileSync(outPath, xlam);
  const sha = createHash("sha256").update(xlam).digest("hex");
  console.log(`${outPath}: ${xlam.length} bytes, sha256 ${sha}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) main(process.argv.slice(2));
