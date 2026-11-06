// Independent readers for the Excel add-in (tests only): zip, [MS-CFB] compound file, [MS-OVBA]
// decompression, dir stream and PROJECT data decryption. Written from the specs, separately from
// the writer in scripts/build-xlam.mjs, and strict: anything off-spec throws.
import { inflateRawSync, crc32 } from "node:zlib";

const fail = (msg) => {
  throw new Error(msg);
};

/* ------------------------------------------------------------------ zip */

/** Map name → Buffer. Checks the central directory against the local headers and the CRCs. */
export function readZip(buf) {
  let eocd = -1;
  for (let i = buf.length - 22; i >= Math.max(0, buf.length - 22 - 0xffff); i--) {
    if (buf.readUInt32LE(i) === 0x06054b50) {
      eocd = i;
      break;
    }
  }
  if (eocd < 0) fail("zip: no end of central directory");
  const count = buf.readUInt16LE(eocd + 10);
  let p = buf.readUInt32LE(eocd + 16);
  const files = new Map();
  for (let i = 0; i < count; i++) {
    if (buf.readUInt32LE(p) !== 0x02014b50) fail("zip: bad central header");
    const method = buf.readUInt16LE(p + 10);
    const crc = buf.readUInt32LE(p + 16);
    const csize = buf.readUInt32LE(p + 20);
    const usize = buf.readUInt32LE(p + 24);
    const nlen = buf.readUInt16LE(p + 28);
    const xlen = buf.readUInt16LE(p + 30);
    const clen = buf.readUInt16LE(p + 32);
    const off = buf.readUInt32LE(p + 42);
    const name = buf.toString("utf8", p + 46, p + 46 + nlen);
    if (buf.readUInt32LE(off) !== 0x04034b50) fail(`zip: bad local header for ${name}`);
    if (buf.toString("utf8", off + 30, off + 30 + buf.readUInt16LE(off + 26)) !== name) fail(`zip: name mismatch ${name}`);
    const start = off + 30 + buf.readUInt16LE(off + 26) + buf.readUInt16LE(off + 28);
    const raw = buf.subarray(start, start + csize);
    const data = method === 0 ? Buffer.from(raw) : method === 8 ? inflateRawSync(raw) : fail(`zip: method ${method}`);
    if (data.length !== usize || crc32(data) !== crc) fail(`zip: size/CRC mismatch for ${name}`);
    if (files.has(name)) fail(`zip: duplicate ${name}`);
    files.set(name, data);
    p += 46 + nlen + xlen + clen;
  }
  return files;
}

/* ------------------------------------------------------------- [MS-CFB] */

const ENDOFCHAIN = 0xfffffffe;
const FREESECT = 0xffffffff;
const FATSECT = 0xfffffffd;
const NOSTREAM = 0xffffffff;

/**
 * Reads a version 3 or 4 compound file without DIFAT sectors beyond the header. Returns
 * { streams: Map(path → Buffer), entries } where a path is "VBA/dir". Validates the sibling trees
 * (order, red-black colouring) and that every sector is used by exactly one chain.
 */
export function readCfb(buf) {
  if (buf.toString("hex", 0, 8) !== "d0cf11e0a1b11ae1") fail("cfb: bad signature");
  if (buf.readUInt16LE(28) !== 0xfffe) fail("cfb: byte order");
  const major = buf.readUInt16LE(26);
  const shift = buf.readUInt16LE(30);
  if (!((major === 3 && shift === 9) || (major === 4 && shift === 12))) fail("cfb: version/sector size");
  if (buf.readUInt16LE(32) !== 6) fail("cfb: mini sector shift");
  if (buf.readUInt32LE(56) !== 4096) fail("cfb: mini stream cutoff");
  const size = 1 << shift;
  const nFat = buf.readUInt32LE(44);
  if (buf.readUInt32LE(68) !== ENDOFCHAIN || buf.readUInt32LE(72) !== 0) fail("cfb: DIFAT sectors not supported");
  const sectorCount = Math.floor((buf.length - size) / size);
  if ((buf.length - size) % size) fail("cfb: file is not a whole number of sectors");
  const sector = (n) => {
    if (n >= sectorCount) fail(`cfb: sector ${n} out of range`);
    return buf.subarray(size + n * size, size + (n + 1) * size);
  };
  const fatSectors = [];
  for (let i = 0; i < 109; i++) {
    const v = buf.readUInt32LE(76 + i * 4);
    if (i < nFat) fatSectors.push(v);
    else if (v !== FREESECT) fail("cfb: unused DIFAT entry not FREESECT");
  }
  const fat = [];
  for (const s of fatSectors) {
    const b = sector(s);
    for (let i = 0; i < size; i += 4) fat.push(b.readUInt32LE(i));
  }
  for (const s of fatSectors) if (fat[s] !== FATSECT) fail("cfb: FAT sector not marked FATSECT");
  const owner = new Map();
  const chain = (first, what) => {
    const out = [];
    for (let s = first; s !== ENDOFCHAIN; s = fat[s]) {
      if (s >= fat.length || s >= sectorCount) fail(`cfb: ${what}: bad sector ${s}`);
      if (owner.has(s)) fail(`cfb: sector ${s} used by ${owner.get(s)} and ${what}`);
      owner.set(s, what);
      out.push(s);
    }
    return out;
  };
  const readChain = (first, what) => Buffer.concat(chain(first, what).map(sector));

  const dirBuf = readChain(buf.readUInt32LE(48), "directory");
  const entries = [];
  for (let o = 0; o < dirBuf.length; o += 128) {
    const nameLen = dirBuf.readUInt16LE(o + 64);
    const type = dirBuf[o + 66];
    const e = {
      name: nameLen ? dirBuf.toString("utf16le", o, o + nameLen - 2) : "",
      type,
      color: dirBuf[o + 67],
      left: dirBuf.readUInt32LE(o + 68),
      right: dirBuf.readUInt32LE(o + 72),
      child: dirBuf.readUInt32LE(o + 76),
      start: dirBuf.readUInt32LE(o + 116),
      size: dirBuf.readUInt32LE(o + 120) + dirBuf.readUInt32LE(o + 124) * 2 ** 32,
    };
    if (nameLen && dirBuf.readUInt16LE(o + nameLen - 2) !== 0) fail(`cfb: name not terminated: ${e.name}`);
    if (type === 0 && (e.left !== NOSTREAM || e.right !== NOSTREAM || e.child !== NOSTREAM)) fail("cfb: free entry with links");
    entries.push(e);
  }
  const rootEntry = entries[0];
  if (rootEntry.type !== 5 || rootEntry.name !== "Root Entry") fail("cfb: no root entry");

  const miniStream = rootEntry.size ? readChain(rootEntry.start, "mini stream") : Buffer.alloc(0);
  const nMiniFat = buf.readUInt32LE(64);
  const miniFatBuf = nMiniFat ? readChain(buf.readUInt32LE(60), "mini FAT") : Buffer.alloc(0);
  if (miniFatBuf.length !== nMiniFat * size) fail("cfb: mini FAT sector count");
  const miniFat = [];
  for (let i = 0; i < miniFatBuf.length; i += 4) miniFat.push(miniFatBuf.readUInt32LE(i));
  const miniOwner = new Map();

  const readStream = (e, path) => {
    if (e.size === 0) return Buffer.alloc(0);
    if (e.size < 4096) {
      const parts = [];
      for (let s = e.start; s !== ENDOFCHAIN; s = miniFat[s]) {
        if (s >= miniFat.length || (s + 1) * 64 > miniStream.length) fail(`cfb: ${path}: bad mini sector ${s}`);
        if (miniOwner.has(s)) fail(`cfb: mini sector ${s} shared`);
        miniOwner.set(s, path);
        parts.push(miniStream.subarray(s * 64, (s + 1) * 64));
      }
      const data = Buffer.concat(parts);
      if (data.length < e.size || data.length - e.size >= 64) fail(`cfb: ${path}: mini chain length`);
      return data.subarray(0, e.size);
    }
    const data = readChain(e.start, path);
    if (data.length < e.size || data.length - e.size >= size) fail(`cfb: ${path}: chain length`);
    return data.subarray(0, e.size);
  };

  const compare = (a, b) => (a.length !== b.length ? a.length - b.length : a.toUpperCase() < b.toUpperCase() ? -1 : a.toUpperCase() > b.toUpperCase() ? 1 : 0);
  const streams = new Map();
  const seen = new Set();
  // Returns the black height of the subtree; checks order and that no red node has a red child.
  const walkTree = (id, prefix, lo, hi, parentRed) => {
    if (id === NOSTREAM) return 1;
    if (id >= entries.length || seen.has(id)) fail(`cfb: bad or repeated entry ${id}`);
    seen.add(id);
    const e = entries[id];
    if (e.type !== 1 && e.type !== 2) fail(`cfb: entry ${id} type ${e.type}`);
    if ((lo !== null && compare(lo, e.name) >= 0) || (hi !== null && compare(e.name, hi) >= 0)) fail(`cfb: ${e.name} out of order`);
    const red = e.color === 0;
    if (red && parentRed) fail(`cfb: red ${e.name} under a red parent`);
    const l = walkTree(e.left, prefix, lo, e.name, red);
    const r = walkTree(e.right, prefix, e.name, hi, red);
    if (l !== r) fail(`cfb: unequal black heights under ${e.name}`);
    const path = prefix + e.name;
    if (e.type === 2) {
      if (e.child !== NOSTREAM) fail(`cfb: stream ${path} has a child`);
      streams.set(path, readStream(e, path));
    } else {
      if (e.start !== 0 || e.size !== 0) fail(`cfb: storage ${path} has data`);
      const kid = entries[e.child];
      if (kid && kid.color === 0) fail(`cfb: subtree root under ${path} is red`);
      walkTree(e.child, path + "/", null, null, false);
    }
    return l + (red ? 0 : 1);
  };
  if (rootEntry.child !== NOSTREAM && entries[rootEntry.child].color === 0) fail("cfb: root subtree is red");
  walkTree(rootEntry.child, "", null, null, false);
  entries.forEach((e, i) => {
    if (i > 0 && e.type !== 0 && !seen.has(i)) fail(`cfb: entry ${e.name} not reachable`);
  });
  // Every sector is a FAT sector or in exactly one chain; the rest are free.
  fat.forEach((v, s) => {
    if (s >= sectorCount) {
      if (v !== FREESECT) fail(`cfb: FAT entry ${s} beyond the file`);
    } else if (v === FATSECT) {
      if (!fatSectors.includes(s)) fail(`cfb: sector ${s} marked FATSECT`);
    } else if (!owner.has(s) && v !== FREESECT) fail(`cfb: sector ${s} allocated but unused`);
  });
  return { streams, entries, major };
}

/* ------------------------------------------------- [MS-OVBA] 2.4.1 */

/** Decompresses a CompressedContainer ([MS-OVBA] 2.4.1.3.1). */
export function ovbaDecompress(buf) {
  if (buf[0] !== 0x01) fail("ovba: bad signature byte");
  const out = [];
  let p = 1;
  while (p < buf.length) {
    if (p + 2 > buf.length) fail("ovba: truncated chunk header");
    const header = buf.readUInt16LE(p);
    const chunkSize = (header & 0x0fff) + 3;
    if (((header >>> 12) & 0x7) !== 0b011) fail("ovba: bad chunk signature");
    const compressed = header >>> 15;
    const chunkEnd = Math.min(buf.length, p + chunkSize);
    p += 2;
    const start = out.length;
    if (!compressed) {
      if (chunkSize !== 4098 || p + 4096 > buf.length) fail("ovba: bad raw chunk");
      for (let i = 0; i < 4096; i++) out.push(buf[p + i]);
      p += 4096;
      continue;
    }
    while (p < chunkEnd) {
      const flags = buf[p++];
      for (let bit = 0; bit < 8 && p < chunkEnd; bit++) {
        if (!((flags >> bit) & 1)) {
          out.push(buf[p++]);
          continue;
        }
        if (p + 2 > chunkEnd) fail("ovba: truncated copy token");
        const token = buf.readUInt16LE(p);
        p += 2;
        const diff = out.length - start;
        let bitCount = 4;
        while (1 << bitCount < diff) bitCount++;
        const lengthMask = 0xffff >>> bitCount;
        const length = (token & lengthMask) + 3;
        const offset = (token >>> (16 - bitCount)) + 1;
        if (offset > diff) fail("ovba: copy token before chunk start");
        for (let i = 0; i < length; i++) out.push(out[out.length - offset]);
      }
    }
    if (out.length - start > 4096) fail("ovba: chunk decompresses to more than 4096 bytes");
  }
  return Buffer.from(out);
}

/* ---------------------------------------- [MS-OVBA] 2.3.4.2 dir stream */

/** Parses the decompressed dir stream. Fails on any record out of the order of the spec. */
export function parseDir(buf) {
  let p = 0;
  const u16 = () => {
    const v = buf.readUInt16LE(p);
    p += 2;
    return v;
  };
  const u32 = () => {
    const v = buf.readUInt32LE(p);
    p += 4;
    return v;
  };
  const bytes = (n) => {
    const b = buf.subarray(p, p + n);
    if (b.length !== n) fail("dir: truncated");
    p += n;
    return b;
  };
  const expect = (id, what) => {
    const got = u16();
    if (got !== id) fail(`dir: expected ${what} (0x${id.toString(16)}), got 0x${got.toString(16)} at ${p - 2}`);
  };
  const peek = () => buf.readUInt16LE(p);
  const fixed = (id, what, size) => {
    expect(id, what);
    if (u32() !== size) fail(`dir: ${what} size`);
    return size === 2 ? u16() : u32();
  };
  const text = (id, what) => {
    expect(id, what);
    return bytes(u32()).toString("latin1");
  };
  const pairText = (id, reserved, what, unicode = true) => {
    const a = text(id, what);
    expect(reserved, `${what} reserved`);
    const b = bytes(u32());
    return { mbcs: a, unicode: unicode ? b.toString("utf16le") : b.toString("latin1") };
  };

  const d = {};
  d.sysKind = fixed(0x0001, "PROJECTSYSKIND", 4);
  if (peek() === 0x004a) d.compatVersion = fixed(0x004a, "PROJECTCOMPATVERSION", 4);
  d.lcid = fixed(0x0002, "PROJECTLCID", 4);
  d.lcidInvoke = fixed(0x0014, "PROJECTLCIDINVOKE", 4);
  d.codePage = fixed(0x0003, "PROJECTCODEPAGE", 2);
  d.name = text(0x0004, "PROJECTNAME");
  d.docString = pairText(0x0005, 0x0040, "PROJECTDOCSTRING");
  d.helpFile = pairText(0x0006, 0x003d, "PROJECTHELPFILEPATH", false);
  d.helpContext = fixed(0x0007, "PROJECTHELPCONTEXT", 4);
  d.libFlags = fixed(0x0008, "PROJECTLIBFLAGS", 4);
  expect(0x0009, "PROJECTVERSION");
  if (u32() !== 4) fail("dir: PROJECTVERSION reserved");
  d.version = { major: u32(), minor: u16() };
  d.constants = pairText(0x000c, 0x003c, "PROJECTCONSTANTS");

  d.references = [];
  while (peek() !== 0x000f) {
    const ref = {};
    if (peek() === 0x0016) ref.name = pairText(0x0016, 0x003e, "REFERENCENAME");
    const id = u16();
    if (id === 0x000d) {
      const size = u32();
      const start = p;
      ref.libid = bytes(u32()).toString("latin1");
      if (u32() !== 0 || u16() !== 0) fail("dir: REFERENCEREGISTERED reserved");
      if (p - start !== size) fail("dir: REFERENCEREGISTERED size");
    } else fail(`dir: unsupported reference record 0x${id.toString(16)}`);
    d.references.push(ref);
  }

  expect(0x000f, "PROJECTMODULES");
  if (u32() !== 2) fail("dir: PROJECTMODULES size");
  const count = u16();
  d.cookie = fixed(0x0013, "PROJECTCOOKIE", 2);
  d.modules = [];
  for (let i = 0; i < count; i++) {
    const m = {};
    m.name = text(0x0019, "MODULENAME");
    if (peek() === 0x0047) {
      expect(0x0047, "MODULENAMEUNICODE");
      m.nameUnicode = bytes(u32()).toString("utf16le");
    }
    m.streamName = pairText(0x001a, 0x0032, "MODULESTREAMNAME");
    m.docString = pairText(0x001c, 0x0048, "MODULEDOCSTRING");
    m.offset = fixed(0x0031, "MODULEOFFSET", 4);
    m.helpContext = fixed(0x001e, "MODULEHELPCONTEXT", 4);
    m.cookie = fixed(0x002c, "MODULECOOKIE", 2);
    const type = u16();
    if (type !== 0x0021 && type !== 0x0022) fail(`dir: MODULETYPE 0x${type.toString(16)}`);
    m.type = type === 0x0021 ? "procedural" : "document";
    if (u32() !== 0) fail("dir: MODULETYPE reserved");
    if (peek() === 0x0025) (u16(), u32(), (m.readOnly = true));
    if (peek() === 0x0028) (u16(), u32(), (m.private = true));
    expect(0x002b, "MODULE Terminator");
    if (u32() !== 0) fail("dir: MODULE terminator reserved");
    d.modules.push(m);
  }
  expect(0x0010, "dir Terminator");
  if (u32() !== 0) fail("dir: terminator reserved");
  if (p !== buf.length) fail(`dir: ${buf.length - p} trailing bytes`);
  return d;
}

/* ---------------------------------------- [MS-OVBA] 2.4.3.3 decryption */

/** Decrypts a CMG / DPB / GC value (hex). Returns { version, projKey, data }. */
export function ovbaDecrypt(hex) {
  if (!/^([0-9A-F]{2})+$/.test(hex)) fail("decrypt: not upper-case hex");
  const enc = Buffer.from(hex, "hex");
  const seed = enc[0];
  const version = seed ^ enc[1];
  const projKey = seed ^ enc[2];
  let unencrypted1 = projKey;
  let encrypted1 = enc[2];
  let encrypted2 = enc[1];
  let p = 3;
  const next = () => {
    if (p >= enc.length) fail("decrypt: truncated");
    const e = enc[p++];
    const b = e ^ ((encrypted2 + unencrypted1) & 0xff);
    encrypted2 = encrypted1;
    encrypted1 = e;
    unencrypted1 = b;
    return b;
  };
  for (let i = 0; i < (seed & 6) / 2; i++) next();
  const len = next() | (next() << 8) | (next() << 16) | (next() << 24);
  const data = Buffer.alloc(len);
  for (let i = 0; i < len; i++) data[i] = next();
  if (p !== enc.length) fail("decrypt: trailing bytes");
  return { version, projKey, data };
}
