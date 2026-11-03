// A VBA-subset -> JavaScript transpiler with a VBA-semantics runtime, used to run
// vba/*.bas without Excel. It deliberately supports only the subset that the
// Tafqit modules use, and rejects anything else at "compile" time instead of
// guessing. Notable VBA semantics implemented:
//
// - case-insensitive identifiers; Option Explicit (undeclared names are errors)
// - parameters are ByRef unless ByVal; a ByRef variable argument must have the
//   same declared type as the parameter ("ByRef argument type mismatch")
// - typed variables with default values and coercion on assignment (Long rounds
//   half to even and overflows past 32 bits; Integer past 16 bits)
// - Integer/Long arithmetic overflow, \ and Mod with rounded operands
// - 1-based Mid/InStr, Left/Right, AscW returning a signed 16-bit value, ChrW
// - non-short-circuit And/Or, binary string comparison (Option Compare Binary)
// - arrays with explicit bounds, multi-dimensional, ReDim [Preserve]
// - Function return through the function name, Exit Function/Sub/For/Do
// - Optional parameters with defaults, IsMissing
//
// Anything locale-dependent (CStr/implicit conversion of non-integral numbers,
// string <-> number coercion) throws, so the VBA code cannot rely on it.

// ------------------------------------------------------------------ values

export const EMPTY = Object.freeze({ toString: () => "[Empty]" });
export const MISSING = Object.freeze({ toString: () => "[Missing]" });
/** Variant/Decimal: holds the exact decimal text, e.g. "1250.75". */
export class VDecimal {
  constructor(text) { this.text = text; }
}
/** Variant/Error value (CVErr), e.g. a #N/A cell. */
export class VErrorValue {
  constructor(code) { this.code = code; }
}
/** Base class of host objects (e.g. a fake Range); members are looked up case-insensitively. */
export class VObject {}
/** Variant holding a typed integer (VarType 2/3) rather than a Double. */
export class VTyped {
  constructor(vt, value) { this.vt = vt; this.value = value; }
}

export class VbaRuntimeError extends Error {
  constructor(number, message) {
    super(`VBA run-time error ${number}: ${message}`);
    this.number = number;
  }
}
export class VbaCompileError extends Error {}

const fail = (n, m) => { throw new VbaRuntimeError(n, m); };
const unsupported = (m) => { throw new VbaRuntimeError(9999, `unsupported in harness: ${m}`); };

class VArray {
  constructor(type, bounds) {
    this.type = type;
    this.bounds = bounds; // [[lb, ub], ...]
    let n = 1;
    for (const [lb, ub] of bounds) {
      if (ub < lb) fail(9, "Subscript out of range");
      n *= ub - lb + 1;
    }
    this.data = Array.from({ length: n }, () => defaultValue(type));
  }
  offset(idx) {
    if (idx.length !== this.bounds.length) fail(9, "Subscript out of range (dimensions)");
    let off = 0;
    for (let d = 0; d < idx.length; d++) {
      const i = toLong(idx[d]);
      const [lb, ub] = this.bounds[d];
      if (i < lb || i > ub) fail(9, `Subscript out of range (${i} not in ${lb}..${ub})`);
      off = off * (ub - lb + 1) + (i - lb);
    }
    return off;
  }
}

/** A dynamic array variable before its first ReDim. */
class DynArray {
  constructor(type) { this.type = type; this.arr = null; }
}

function defaultValue(type) {
  switch (type) {
    case "string": return "";
    case "boolean": return false;
    case "variant": return EMPTY;
    case "long": case "integer": case "byte": case "double": case "single": case "currency": return 0;
    default: unsupported(`type ${type}`);
  }
}

// ------------------------------------------------------------------ conversions

function bankersRound(x) {
  const f = Math.floor(x);
  const d = x - f;
  if (d > 0.5) return f + 1;
  if (d < 0.5) return f;
  return f % 2 === 0 ? f : f + 1;
}

function num(v) {
  if (typeof v === "number") return v;
  if (typeof v === "boolean") return v ? -1 : 0;
  if (v === EMPTY) return 0;
  if (v instanceof VTyped) return v.value;
  if (v instanceof VDecimal) return Number(v.text);
  if (typeof v === "string") unsupported(`implicit string -> number conversion of "${v}" (locale-dependent)`);
  return fail(13, "Type mismatch");
}

function toLong(v) {
  const n = bankersRound(num(v));
  if (n < -2147483648 || n > 2147483647) fail(6, "Overflow");
  return n;
}
function toInteger(v) {
  const n = bankersRound(num(v));
  if (n < -32768 || n > 32767) fail(6, "Overflow");
  return n;
}

function toStr(v) {
  if (typeof v === "string") return v;
  if (v === EMPTY) return "";
  if (v instanceof VTyped) return String(v.value);
  if (typeof v === "number") {
    if (Number.isInteger(v) && Math.abs(v) < 1e15) return String(v);
    unsupported(`implicit conversion of ${v} to String (locale-dependent)`);
  }
  if (typeof v === "boolean") unsupported("implicit Boolean -> String (locale-dependent)");
  if (v instanceof VDecimal) unsupported("implicit Decimal -> String (locale-dependent); use Str");
  return fail(13, "Type mismatch");
}

function toBool(v) {
  if (typeof v === "boolean") return v;
  if (typeof v === "number") return v !== 0;
  if (v === EMPTY) return false;
  if (v instanceof VTyped) return v.value !== 0;
  if (typeof v === "string") unsupported("String used as Boolean");
  return fail(13, "Type mismatch");
}

function coerce(type, v) {
  if (v instanceof VArray || v instanceof DynArray) fail(13, "Type mismatch (array)");
  switch (type) {
    case "variant": return v;
    case "string": return toStr(v);
    case "long": return toLong(v);
    case "integer": return toInteger(v);
    case "boolean": return toBool(v);
    case "double": return num(v);
    default: return unsupported(`coercion to ${type}`);
  }
}

function varType(v) {
  if (typeof v === "string") return 8;
  if (typeof v === "boolean") return 11;
  if (typeof v === "number") return 5;
  if (v === EMPTY) return 0;
  if (v === MISSING) return 10;
  if (v instanceof VTyped) return v.vt;
  if (v instanceof VDecimal) return 14;
  if (v instanceof VErrorValue) return 10;
  if (v instanceof VObject) return 9;
  if (v instanceof VArray) return 8192 + 12;
  return fail(13, "Type mismatch");
}

/** CDec(Double): VarDecFromR8 keeps 15 significant digits. */
function doubleToDecimalText(x) {
  if (!Number.isFinite(x)) fail(6, "Overflow");
  if (Math.abs(x) >= 7.9228162514264337593543950335e28) fail(6, "Overflow");
  if (x === 0) return "0";
  const [mant, expS] = x.toExponential(14).split("e");
  const neg = mant.startsWith("-");
  const digits = mant.replace(/^-/, "").replace(".", "");
  const e = Number(expS);
  let intPart, frac;
  if (e >= 0) {
    const all = digits.padEnd(e + 1, "0");
    intPart = all.slice(0, e + 1);
    frac = all.slice(e + 1);
  } else {
    intPart = "0";
    frac = "0".repeat(-e - 1) + digits;
  }
  frac = frac.replace(/0+$/, "");
  if (frac.length > 28) frac = frac.slice(0, 28).replace(/0+$/, ""); // Decimal scale <= 28
  const t = (intPart.replace(/^0+(?=\d)/, "") || "0") + (frac ? "." + frac : "");
  return (neg && t !== "0" ? "-" : "") + t;
}

// ------------------------------------------------------------------ runtime operators

function isNumLike(v) {
  return typeof v === "number" || typeof v === "boolean" || v instanceof VTyped || v instanceof VDecimal;
}

function compare(op, a, b) {
  let r;
  if (a === EMPTY && typeof b === "string") a = "";
  if (b === EMPTY && typeof a === "string") b = "";
  if (typeof a === "string" && typeof b === "string") {
    r = a < b ? -1 : a > b ? 1 : 0; // binary (UTF-16 code unit) comparison
  } else if ((isNumLike(a) || a === EMPTY) && (isNumLike(b) || b === EMPTY)) {
    const x = num(a), y = num(b);
    r = x < y ? -1 : x > y ? 1 : 0;
  } else {
    return unsupported(`comparison of ${varType(a)} with ${varType(b)}`);
  }
  switch (op) {
    case "=": return r === 0;
    case "<>": return r !== 0;
    case "<": return r < 0;
    case ">": return r > 0;
    case "<=": return r <= 0;
    case ">=": return r >= 0;
  }
  return unsupported(op);
}

function intResult(kind, n) {
  if (kind === "integer" && (n < -32768 || n > 32767)) fail(6, "Overflow");
  if (kind === "long" && (n < -2147483648 || n > 2147483647)) fail(6, "Overflow");
  return n;
}

function arith(op, kind, a, b) {
  if (op === "+" && typeof a === "string" && typeof b === "string") return a + b;
  const x = num(a), y = num(b);
  switch (op) {
    case "+": return intResult(kind, x + y);
    case "-": return intResult(kind, x - y);
    case "*": return intResult(kind, x * y);
    case "/": if (y === 0) fail(11, "Division by zero"); return x / y;
    case "^": return Math.pow(x, y);
    case "\\": {
      const p = toLong(x), q = toLong(y);
      if (q === 0) fail(11, "Division by zero");
      return Math.trunc(p / q);
    }
    case "mod": {
      const p = toLong(x), q = toLong(y);
      if (q === 0) fail(11, "Division by zero");
      return p % q;
    }
  }
  return unsupported(op);
}

function logic(op, a, b) {
  if (typeof a === "boolean" && typeof b === "boolean") {
    if (op === "and") return a && b;
    if (op === "or") return a || b;
    if (op === "xor") return a !== b;
  }
  const x = toLong(a), y = toLong(b);
  if (op === "and") return x & y;
  if (op === "or") return x | y;
  if (op === "xor") return x ^ y;
  return unsupported(op);
}

function not(a) {
  if (typeof a === "boolean") return !a;
  return ~toLong(a);
}

function neg(a) {
  if (a instanceof VDecimal) return new VDecimal(a.text.startsWith("-") ? a.text.slice(1) : "-" + a.text);
  return -num(a);
}

function needStr(v, fn) {
  if (typeof v === "string") return v;
  if (v === EMPTY) return "";
  if (v instanceof VTyped || (typeof v === "number" && Number.isInteger(v))) return toStr(v);
  return unsupported(`${fn}() of a non-string`);
}

function member(obj, name, args) {
  if (!(obj instanceof VObject)) fail(424, "Object required");
  const lname = name.toLowerCase();
  let key;
  for (let p = obj; p && p !== Object.prototype; p = Object.getPrototypeOf(p)) {
    key = Object.getOwnPropertyNames(p).find((k) => k.toLowerCase() === lname);
    if (key) break;
  }
  if (!key) fail(438, `Object doesn't support this property or method: ${name}`);
  const v = obj[key];
  if (typeof v === "function") return v.apply(obj, args ?? []);
  if (args && args.length) unsupported(`indexed property ${name}`);
  return v;
}

// ------------------------------------------------------------------ builtins

const BUILTINS = {
  len: { type: "long", f: (s) => needStr(s, "Len").length },
  mid: {
    type: "string",
    f: (s, start, n) => {
      s = needStr(s, "Mid");
      start = toLong(start);
      if (start < 1) fail(5, "Invalid procedure call (Mid start)");
      if (n === undefined || n === MISSING) return s.slice(start - 1);
      n = toLong(n);
      if (n < 0) fail(5, "Invalid procedure call (Mid length)");
      return s.substr(start - 1, n);
    },
  },
  left: {
    type: "string",
    f: (s, n) => {
      s = needStr(s, "Left");
      n = toLong(n);
      if (n < 0) fail(5, "Invalid procedure call (Left)");
      return s.slice(0, n);
    },
  },
  right: {
    type: "string",
    f: (s, n) => {
      s = needStr(s, "Right");
      n = toLong(n);
      if (n < 0) fail(5, "Invalid procedure call (Right)");
      return n === 0 ? "" : s.slice(Math.max(0, s.length - n));
    },
  },
  instr: {
    type: "long",
    f: (...a) => {
      let start = 1, s1, s2;
      if (a.length === 3) [start, s1, s2] = [toLong(a[0]), a[1], a[2]];
      else if (a.length === 2) [s1, s2] = a;
      else unsupported("InStr with compare argument");
      s1 = needStr(s1, "InStr");
      s2 = needStr(s2, "InStr");
      if (start < 1) fail(5, "Invalid procedure call (InStr)");
      if (start > s1.length) return 0;
      if (s2 === "") return start;
      return s1.indexOf(s2, start - 1) + 1;
    },
  },
  trim: { type: "string", f: (s) => needStr(s, "Trim").replace(/^ +| +$/g, "") },
  ltrim: { type: "string", f: (s) => needStr(s, "LTrim").replace(/^ +/, "") },
  rtrim: { type: "string", f: (s) => needStr(s, "RTrim").replace(/ +$/, "") },
  ucase: {
    type: "string",
    f: (s) => {
      s = needStr(s, "UCase");
      if (!/^[\x00-\x7f]*$/.test(s)) unsupported("UCase of non-ASCII text (locale-dependent)");
      return s.toUpperCase();
    },
  },
  lcase: {
    type: "string",
    f: (s) => {
      s = needStr(s, "LCase");
      if (!/^[\x00-\x7f]*$/.test(s)) unsupported("LCase of non-ASCII text (locale-dependent)");
      return s.toLowerCase();
    },
  },
  chrw: {
    type: "string",
    f: (n) => {
      n = toLong(n);
      if (n < -32768 || n > 65535) fail(5, "Invalid procedure call (ChrW)");
      return String.fromCharCode(n < 0 ? n + 65536 : n);
    },
  },
  ascw: {
    type: "integer",
    f: (s) => {
      s = needStr(s, "AscW");
      if (s === "") fail(5, "Invalid procedure call (AscW of empty string)");
      const c = s.charCodeAt(0);
      return c > 32767 ? c - 65536 : c;
    },
  },
  hex: {
    type: "string",
    f: (n) => {
      n = toLong(n);
      return (n < 0 ? n + 4294967296 : n).toString(16).toUpperCase();
    },
  },
  str: {
    type: "string",
    f: (v) => {
      if (v instanceof VDecimal) return v.text.startsWith("-") ? v.text : " " + v.text;
      if (v instanceof VTyped) v = v.value;
      if (typeof v === "number" && Number.isInteger(v) && Math.abs(v) < 1e15) return (v < 0 ? "" : " ") + String(v);
      return unsupported(`Str of ${typeof v} ${String(v)}`);
    },
  },
  string: {
    type: "string",
    f: (n, ch) => {
      n = toLong(n);
      if (n < 0) fail(5, "Invalid procedure call (String)");
      ch = typeof ch === "string" ? ch : String.fromCharCode(toLong(ch));
      if (ch === "") fail(5, "Invalid procedure call (String)");
      return ch[0].repeat(n);
    },
  },
  cdec: {
    type: "variant",
    f: (v) => {
      if (v instanceof VDecimal) return v;
      if (v instanceof VTyped) return new VDecimal(String(v.value));
      if (typeof v === "number") return new VDecimal(doubleToDecimalText(v));
      if (typeof v === "boolean") return new VDecimal(v ? "-1" : "0");
      if (v === EMPTY) return new VDecimal("0");
      return unsupported("CDec of a string (locale-dependent)");
    },
  },
  clng: { type: "long", f: (v) => toLong(v) },
  cint: { type: "integer", f: (v) => toInteger(v) },
  cbool: { type: "boolean", f: (v) => toBool(v) },
  cstr: {
    type: "string",
    f: (v) => {
      if (typeof v === "string") return v;
      return toStr(v);
    },
  },
  vartype: { type: "long", f: (v) => varType(v) },
  isobject: { type: "boolean", f: (v) => v instanceof VObject },
  iserror: { type: "boolean", f: (v) => v instanceof VErrorValue || v === MISSING },
  ismissing: { type: "boolean", f: (v) => v === MISSING },
  isempty: { type: "boolean", f: (v) => v === EMPTY },
  ubound: {
    type: "long",
    f: (a, d) => {
      a = a instanceof DynArray ? a.arr : a;
      if (!(a instanceof VArray)) fail(9, "Subscript out of range (UBound)");
      return a.bounds[(d === undefined ? 1 : toLong(d)) - 1][1];
    },
  },
  lbound: {
    type: "long",
    f: (a, d) => {
      a = a instanceof DynArray ? a.arr : a;
      if (!(a instanceof VArray)) fail(9, "Subscript out of range (LBound)");
      return a.bounds[(d === undefined ? 1 : toLong(d)) - 1][0];
    },
  },
  cverr: { type: "variant", f: (n) => new VErrorValue(toLong(n)) },
};

const CONSTANTS = {
  true: [true, "boolean"], false: [false, "boolean"], empty: [EMPTY, "variant"],
  vbempty: [0, "long"], vbnull: [1, "long"], vbinteger: [2, "long"], vblong: [3, "long"], vbsingle: [4, "long"],
  vbdouble: [5, "long"], vbcurrency: [6, "long"], vbdate: [7, "long"], vbstring: [8, "long"], vbobject: [9, "long"],
  vberror: [10, "long"], vbboolean: [11, "long"], vbvariant: [12, "long"], vbdecimal: [14, "long"], vbbyte: [17, "long"],
  vbcrlf: ["\r\n", "string"], vbcr: ["\r", "string"], vblf: ["\n", "string"], vbnewline: ["\r\n", "string"],
  vbtab: ["\t", "string"], vbnullstring: ["", "string"],
  vbokonly: [0, "long"], vbcritical: [16, "long"], vbquestion: [32, "long"], vbexclamation: [48, "long"], vbinformation: [64, "long"],
};

// ------------------------------------------------------------------ lexer

const KEYWORDS = new Set([
  "and", "or", "not", "xor", "mod", "if", "then", "else", "elseif", "end", "for", "to", "step", "next", "do", "loop",
  "while", "until", "wend", "select", "case", "is", "function", "sub", "dim", "redim", "preserve", "as", "byval",
  "byref", "optional", "private", "public", "const", "exit", "call", "option", "explicit", "set", "let", "new",
  "each", "in", "goto", "on", "resume", "with", "static", "global", "type", "enum", "like", "eqv", "imp", "attribute",
]);

function lex(line, where) {
  const toks = [];
  let i = 0;
  while (i < line.length) {
    const ch = line[i];
    if (ch === " " || ch === "\t") { i++; continue; }
    if (ch === "'") break;
    if (ch === '"') {
      let j = i + 1, s = "";
      for (;;) {
        if (j >= line.length) throw new VbaCompileError(`${where}: unterminated string`);
        if (line[j] === '"') {
          if (line[j + 1] === '"') { s += '"'; j += 2; continue; }
          break;
        }
        s += line[j++];
      }
      toks.push({ t: "str", v: s });
      i = j + 1;
      continue;
    }
    let m = /^(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?([&#!%@]?)/.exec(line.slice(i));
    if (m && /[\d.]/.test(ch) && !(ch === "." && !/\d/.test(line[i + 1] ?? ""))) {
      const isFloat = /[.eE]/.test(m[1] + (m[2] ?? ""));
      const v = Number(m[1] + (m[2] ?? ""));
      let type = m[3] === "&" ? "long" : m[3] === "#" ? "double" : m[3] ? null : isFloat ? "double" : v <= 32767 ? "integer" : v <= 2147483647 ? "long" : "double";
      if (!type) throw new VbaCompileError(`${where}: unsupported literal suffix ${m[3]}`);
      toks.push({ t: "num", v, type });
      i += m[0].length;
      continue;
    }
    m = /^[A-Za-z][A-Za-z0-9_]*/.exec(line.slice(i));
    if (m) {
      const lw = m[0].toLowerCase();
      if (lw === "rem" && toks.length === 0) break;
      if (line[i + m[0].length] && /[$%&!#@]/.test(line[i + m[0].length])) throw new VbaCompileError(`${where}: type suffixes on names are not supported`);
      toks.push(KEYWORDS.has(lw) ? { t: "kw", v: lw } : { t: "id", v: lw, raw: m[0] });
      i += m[0].length;
      continue;
    }
    const two = line.slice(i, i + 2);
    if (["<>", "<=", ">=", ":="].includes(two)) { toks.push({ t: "op", v: two }); i += 2; continue; }
    if ("&+-*/\\^=<>(),.:;".includes(ch)) { toks.push({ t: "op", v: ch }); i++; continue; }
    throw new VbaCompileError(`${where}: unexpected character ${JSON.stringify(ch)} (U+${ch.charCodeAt(0).toString(16)})`);
  }
  return toks;
}

// ------------------------------------------------------------------ parser

class Parser {
  constructor(toks, where) { this.toks = toks; this.i = 0; this.where = where; }
  peek(o = 0) { return this.toks[this.i + o]; }
  err(m) { throw new VbaCompileError(`${this.where}: ${m}`); }
  isKw(v, o = 0) { const t = this.peek(o); return t && t.t === "kw" && t.v === v; }
  isOp(v, o = 0) { const t = this.peek(o); return t && t.t === "op" && t.v === v; }
  eatKw(v) { if (this.isKw(v)) { this.i++; return true; } return false; }
  eatOp(v) { if (this.isOp(v)) { this.i++; return true; } return false; }
  needKw(v) { if (!this.eatKw(v)) this.err(`expected ${v}, got ${JSON.stringify(this.peek())}`); }
  needOp(v) { if (!this.eatOp(v)) this.err(`expected "${v}", got ${JSON.stringify(this.peek())}`); }
  ident() {
    const t = this.peek();
    if (!t || t.t !== "id") this.err(`expected identifier, got ${JSON.stringify(t)}`);
    this.i++;
    return t.v;
  }
  atEnd() { return this.i >= this.toks.length || this.isOp(":"); }

  // precedence climbing, lowest first
  expr() { return this.orExpr(); }
  orExpr() {
    let a = this.andExpr();
    for (;;) {
      if (this.eatKw("or")) a = { k: "logic", op: "or", a, b: this.andExpr() };
      else if (this.eatKw("xor")) a = { k: "logic", op: "xor", a, b: this.andExpr() };
      else return a;
    }
  }
  andExpr() {
    let a = this.notExpr();
    while (this.eatKw("and")) a = { k: "logic", op: "and", a, b: this.notExpr() };
    return a;
  }
  notExpr() {
    if (this.eatKw("not")) return { k: "not", a: this.notExpr() };
    return this.cmpExpr();
  }
  cmpExpr() {
    let a = this.catExpr();
    for (;;) {
      const t = this.peek();
      if (t && t.t === "op" && ["=", "<>", "<", ">", "<=", ">="].includes(t.v)) {
        this.i++;
        a = { k: "cmp", op: t.v, a, b: this.catExpr() };
      } else if (this.isKw("like") || this.isKw("is")) this.err("Like/Is not supported");
      else return a;
    }
  }
  catExpr() {
    let a = this.addExpr();
    while (this.eatOp("&")) a = { k: "cat", a, b: this.addExpr() };
    return a;
  }
  addExpr() {
    let a = this.modExpr();
    for (;;) {
      if (this.eatOp("+")) a = { k: "arith", op: "+", a, b: this.modExpr() };
      else if (this.eatOp("-")) a = { k: "arith", op: "-", a, b: this.modExpr() };
      else return a;
    }
  }
  modExpr() {
    let a = this.idivExpr();
    while (this.eatKw("mod")) a = { k: "arith", op: "mod", a, b: this.idivExpr() };
    return a;
  }
  idivExpr() {
    let a = this.mulExpr();
    while (this.eatOp("\\")) a = { k: "arith", op: "\\", a, b: this.mulExpr() };
    return a;
  }
  mulExpr() {
    let a = this.unary();
    for (;;) {
      if (this.eatOp("*")) a = { k: "arith", op: "*", a, b: this.unary() };
      else if (this.eatOp("/")) a = { k: "arith", op: "/", a, b: this.unary() };
      else return a;
    }
  }
  unary() {
    if (this.eatOp("-")) return { k: "neg", a: this.unary() };
    if (this.eatOp("+")) return this.unary();
    return this.pow();
  }
  pow() {
    let a = this.postfix();
    while (this.eatOp("^")) {
      // -2^2 = -4: the exponent may itself be negated
      const b = this.isOp("-") ? (this.i++, { k: "neg", a: this.postfix() }) : this.postfix();
      a = { k: "arith", op: "^", a, b };
    }
    return a;
  }
  args() {
    // after "(": comma-separated, possibly empty (missing) arguments
    const list = [];
    if (this.eatOp(")")) return list;
    for (;;) {
      if (this.isOp(",") || this.isOp(")")) list.push(null);
      else {
        if (this.peek()?.t === "id" && this.isOp(":=", 1)) this.err("named arguments are not supported");
        list.push(this.expr());
      }
      if (this.eatOp(",")) continue;
      this.needOp(")");
      return list;
    }
  }
  postfix() {
    let e = this.primary();
    for (;;) {
      if (this.isOp("(") && (e.k === "name" || e.k === "member")) {
        this.i++;
        e = { ...e, args: this.args() };
        if (e.k === "name") e = { k: "call", name: e.name, args: e.args };
      } else if (this.eatOp(".")) {
        const t = this.peek();
        if (!t || (t.t !== "id" && t.t !== "kw")) this.err("expected member name");
        this.i++;
        e = { k: "member", obj: e, name: t.raw ?? t.v, args: null };
      } else return e;
    }
  }
  primary() {
    const t = this.peek();
    if (!t) this.err("unexpected end of expression");
    if (t.t === "num") { this.i++; return { k: "lit", v: t.v, type: t.type }; }
    if (t.t === "str") { this.i++; return { k: "lit", v: t.v, type: "string" }; }
    if (t.t === "id") { this.i++; return { k: "name", name: t.v }; }
    if (this.eatOp("(")) {
      const e = this.expr();
      this.needOp(")");
      return { k: "paren", a: e };
    }
    return this.err(`unexpected token ${JSON.stringify(t)}`);
  }

  typeClause() {
    if (!this.eatKw("as")) return "variant";
    const t = this.peek();
    this.i++;
    const name = (t.v ?? "").toLowerCase();
    if (!["string", "long", "integer", "boolean", "variant", "double", "byte"].includes(name)) this.err(`unsupported type ${t.v}`);
    return name;
  }
  bounds() {
    // after "(": "(lb To ub, ...)" or "(ub)" or "()"
    const dims = [];
    if (this.eatOp(")")) return dims;
    for (;;) {
      const a = this.expr();
      if (this.eatKw("to")) dims.push([a, this.expr()]);
      else dims.push([{ k: "lit", v: 0, type: "integer" }, a]);
      if (this.eatOp(",")) continue;
      this.needOp(")");
      return dims;
    }
  }
  declList() {
    const vars = [];
    do {
      const name = this.ident();
      let dims = null;
      if (this.eatOp("(")) dims = this.bounds();
      const type = this.typeClause();
      vars.push({ name, dims, type });
    } while (this.eatOp(","));
    return vars;
  }
  params() {
    const list = [];
    this.needOp("(");
    if (this.eatOp(")")) return list;
    for (;;) {
      const p = { optional: false, byRef: true };
      if (this.eatKw("optional")) p.optional = true;
      if (this.eatKw("byval")) p.byRef = false;
      else if (this.eatKw("byref")) p.byRef = true;
      p.name = this.ident();
      p.isArray = false;
      if (this.eatOp("(")) { this.needOp(")"); p.isArray = true; }
      p.type = this.typeClause();
      if (this.eatOp("=")) {
        if (!p.optional) this.err("default value on a required parameter");
        p.def = this.expr();
      }
      list.push(p);
      if (this.eatOp(",")) continue;
      this.needOp(")");
      return list;
    }
  }

  // ---- statements (one line may hold several, separated by ":")
  lineStatements() {
    const out = [];
    while (this.i < this.toks.length) {
      if (this.eatOp(":")) continue;
      out.push(...this.statement());
      if (this.i < this.toks.length && !this.isOp(":") && !this.isKw("else")) this.err(`unexpected ${JSON.stringify(this.peek())}`);
      if (this.isKw("else")) return out; // single-line If caller handles it
    }
    return out;
  }
  simpleUntilElseOrEnd() {
    const out = [];
    while (this.i < this.toks.length && !this.isKw("else")) {
      if (this.eatOp(":")) continue;
      const s = this.statement();
      for (const x of s) if (x.block) this.err("block statement in single-line If");
      out.push(...s);
    }
    return out;
  }
  statement() {
    const t = this.peek();
    const kw = t.t === "kw" ? t.v : null;
    let vis = null;
    if (kw === "private" || kw === "public") {
      vis = kw;
      this.i++;
    }
    const k2 = this.peek()?.t === "kw" ? this.peek().v : null;
    if (k2 === "function" || k2 === "sub") {
      this.i++;
      const name = this.ident();
      const params = this.params();
      const retType = k2 === "function" ? this.typeClause() : null;
      return [{ block: "proc", isFunc: k2 === "function", name, params, retType, vis: vis ?? "public" }];
    }
    if (k2 === "const") {
      this.i++;
      const name = this.ident();
      const type = this.typeClause();
      this.needOp("=");
      return [{ k: "const", name, type, value: this.expr(), vis }];
    }
    if (vis || kw === "dim") {
      if (kw === "dim" || this.isKw("dim")) this.eatKw("dim");
      return [{ k: "dim", vars: this.declList(), vis }];
    }
    switch (kw) {
      case "option":
        this.i++;
        this.needKw("explicit");
        return [{ k: "optionExplicit" }];
      case "redim": {
        this.i++;
        const preserve = this.eatKw("preserve");
        const items = [];
        do {
          const name = this.ident();
          this.needOp("(");
          const dims = this.bounds();
          if (this.isKw("as")) this.typeClause();
          items.push({ name, dims });
        } while (this.eatOp(","));
        return [{ k: "redim", preserve, items }];
      }
      case "if": {
        this.i++;
        const cond = this.expr();
        this.needKw("then");
        if (this.i >= this.toks.length) return [{ block: "if", cond }];
        const thenS = this.simpleUntilElseOrEnd();
        let elseS = [];
        if (this.eatKw("else")) elseS = this.simpleUntilElseOrEnd();
        return [{ k: "ifline", cond, thenS, elseS }];
      }
      case "elseif": {
        this.i++;
        const cond = this.expr();
        this.needKw("then");
        return [{ block: "elseif", cond }];
      }
      case "else":
        this.i++;
        return [{ block: "else" }];
      case "end": {
        this.i++;
        const what = this.peek()?.v;
        this.i++;
        if (!["if", "select", "function", "sub"].includes(what)) this.err(`End ${what} not supported`);
        return [{ block: "end" + what }];
      }
      case "select":
        this.i++;
        this.needKw("case");
        return [{ block: "select", expr: this.expr() }];
      case "case": {
        this.i++;
        if (this.eatKw("else")) return [{ block: "case", items: null }];
        const items = [];
        do {
          if (this.eatKw("is")) {
            const op = this.peek();
            if (op?.t !== "op" || !["=", "<>", "<", ">", "<=", ">="].includes(op.v)) this.err("bad Case Is");
            this.i++;
            items.push({ is: op.v, v: this.catExpr() });
          } else {
            const a = this.expr();
            if (this.eatKw("to")) items.push({ from: a, to: this.expr() });
            else items.push({ v: a });
          }
        } while (this.eatOp(","));
        return [{ block: "case", items }];
      }
      case "for": {
        this.i++;
        if (this.isKw("each")) this.err("For Each not supported");
        const v = this.ident();
        this.needOp("=");
        const from = this.expr();
        this.needKw("to");
        const to = this.expr();
        const step = this.eatKw("step") ? this.expr() : null;
        return [{ block: "for", v, from, to, step }];
      }
      case "next": {
        this.i++;
        const v = this.peek()?.t === "id" ? this.ident() : null;
        return [{ block: "next", v }];
      }
      case "do": {
        this.i++;
        let pre = null;
        if (this.eatKw("while")) pre = { until: false, cond: this.expr() };
        else if (this.eatKw("until")) pre = { until: true, cond: this.expr() };
        return [{ block: "do", pre }];
      }
      case "loop": {
        this.i++;
        let post = null;
        if (this.eatKw("while")) post = { until: false, cond: this.expr() };
        else if (this.eatKw("until")) post = { until: true, cond: this.expr() };
        return [{ block: "loop", post }];
      }
      case "exit": {
        this.i++;
        const what = this.peek()?.v;
        this.i++;
        if (!["function", "sub", "for", "do"].includes(what)) this.err(`Exit ${what} not supported`);
        return [{ k: "exit", what }];
      }
      case "call": {
        this.i++;
        const name = this.ident();
        this.needOp("(");
        return [{ k: "callstmt", name, args: this.args() }];
      }
      case "set": case "let": case "goto": case "on": case "resume": case "with": case "static": case "while": case "wend":
        this.err(`${kw} not supported`);
    }
    if (t.t !== "id") this.err(`unexpected ${JSON.stringify(t)}`);
    // Debug.Print
    if (t.v === "debug" && this.isOp(".", 1)) {
      this.i += 2;
      const m = this.ident();
      if (m !== "print") this.err("only Debug.Print is supported");
      const parts = [];
      while (!this.atEnd() && !this.isKw("else")) {
        parts.push(this.expr());
        if (!this.eatOp(";")) break;
      }
      return [{ k: "print", parts }];
    }
    // assignment or call statement
    const save = this.i;
    const target = this.postfix();
    if (this.eatOp("=")) {
      if (target.k !== "name" && target.k !== "call") this.err("bad assignment target");
      return [{ k: "assign", target, value: this.expr() }];
    }
    if (target.k === "name" || (target.k === "call" && (this.atEnd() || this.isKw("else")))) {
      if (target.k === "call") {
        // "Foo(a)" as a statement: a single parenthesised argument
        if (target.args.length !== 1) this.err("call statement with parentheses: use Call");
        return [{ k: "callstmt", name: target.name, args: [{ k: "paren", a: target.args[0] }] }];
      }
      // "Foo a, b"
      const args = [];
      if (!this.atEnd() && !this.isKw("else")) {
        for (;;) {
          if (this.isOp(",")) args.push(null);
          else args.push(this.expr());
          if (!this.eatOp(",")) break;
        }
      }
      return [{ k: "callstmt", name: target.name, args }];
    }
    if (target.k === "call") {
      // "Foo (a), b" style: re-parse as name + argument list
      this.i = save + 1;
      const args = [];
      for (;;) {
        if (this.isOp(",")) args.push(null);
        else args.push(this.expr());
        if (!this.eatOp(",")) break;
      }
      return [{ k: "callstmt", name: t.v, args }];
    }
    return this.err("unsupported statement");
  }
}

// ------------------------------------------------------------------ module structure

function logicalLines(src, file) {
  const phys = src.split(/\r\n|\n/);
  const out = [];
  let acc = "", start = 0;
  phys.forEach((line, n) => {
    if (acc === "") start = n + 1;
    if (/ _$/.test(line)) {
      acc += line.slice(0, -1) + " ";
      return;
    }
    acc += line;
    out.push({ text: acc, where: `${file}:${start}` });
    acc = "";
  });
  return out;
}

function parseModule(src, file) {
  const mod = { file, name: null, explicit: false, decls: [], consts: [], procs: [] };
  let proc = null;
  const stack = [];
  const push = (s) => {
    const top = stack[stack.length - 1];
    top.target.push(s);
  };
  for (const { text, where } of logicalLines(src, file)) {
    const am = /^Attribute\s+VB_Name\s*=\s*"([^"]+)"\s*$/i.exec(text);
    if (am) { mod.name = am[1]; continue; }
    if (/^\s*Attribute\s/i.test(text)) continue;
    const toks = lex(text, where);
    if (!toks.length) continue;
    const p = new Parser(toks, where);
    const stmts = p.lineStatements();
    if (p.i < toks.length) p.err(`unexpected ${JSON.stringify(p.peek())}`);
    for (const s of stmts) {
      s.where = where;
      if (!proc) {
        if (s.block === "proc") {
          proc = { ...s, body: [], where };
          stack.length = 0;
          stack.push({ kind: "proc", target: proc.body });
        } else if (s.k === "optionExplicit") mod.explicit = true;
        else if (s.k === "dim") mod.decls.push(s);
        else if (s.k === "const") mod.consts.push(s);
        else throw new VbaCompileError(`${where}: statement outside a procedure`);
        continue;
      }
      const top = stack[stack.length - 1];
      switch (s.block) {
        case undefined: push(s); break;
        case "proc": throw new VbaCompileError(`${where}: nested procedure`);
        case "endfunction": case "endsub":
          if (stack.length !== 1 || (s.block === "endfunction") !== proc.isFunc) throw new VbaCompileError(`${where}: unbalanced ${s.block}`);
          mod.procs.push(proc);
          proc = null;
          break;
        case "if": {
          const node = { k: "if", arms: [{ cond: s.cond, body: [] }], elseBody: null, where };
          push(node);
          stack.push({ kind: "if", node, target: node.arms[0].body });
          break;
        }
        case "elseif":
          if (top.kind !== "if" || top.node.elseBody) throw new VbaCompileError(`${where}: ElseIf without If`);
          top.node.arms.push({ cond: s.cond, body: [] });
          top.target = top.node.arms[top.node.arms.length - 1].body;
          break;
        case "else":
          if (top.kind !== "if" || top.node.elseBody) throw new VbaCompileError(`${where}: Else without If`);
          top.node.elseBody = [];
          top.target = top.node.elseBody;
          break;
        case "endif":
          if (top.kind !== "if") throw new VbaCompileError(`${where}: End If without If`);
          stack.pop();
          break;
        case "select": {
          const node = { k: "select", expr: s.expr, cases: [], where };
          push(node);
          stack.push({ kind: "select", node, target: null });
          break;
        }
        case "case": {
          if (top.kind !== "select") throw new VbaCompileError(`${where}: Case outside Select`);
          const c = { items: s.items, body: [] };
          top.node.cases.push(c);
          top.target = c.body;
          break;
        }
        case "endselect":
          if (top.kind !== "select") throw new VbaCompileError(`${where}: End Select without Select`);
          stack.pop();
          break;
        case "for": {
          const node = { k: "for", v: s.v, from: s.from, to: s.to, step: s.step, body: [], where };
          push(node);
          stack.push({ kind: "for", node, target: node.body });
          break;
        }
        case "next":
          if (top.kind !== "for" || (s.v && s.v !== top.node.v)) throw new VbaCompileError(`${where}: Next without matching For`);
          stack.pop();
          break;
        case "do": {
          const node = { k: "do", pre: s.pre, post: null, body: [], where };
          push(node);
          stack.push({ kind: "do", node, target: node.body });
          break;
        }
        case "loop":
          if (top.kind !== "do") throw new VbaCompileError(`${where}: Loop without Do`);
          if (s.post && top.node.pre) throw new VbaCompileError(`${where}: condition on both Do and Loop`);
          top.node.post = s.post;
          stack.pop();
          break;
        default:
          throw new VbaCompileError(`${where}: ${s.block}`);
      }
    }
  }
  if (proc) throw new VbaCompileError(`${file}: missing End ${proc.isFunc ? "Function" : "Sub"}`);
  if (!mod.name) throw new VbaCompileError(`${file}: missing Attribute VB_Name`);
  if (!mod.explicit) throw new VbaCompileError(`${file}: missing Option Explicit`);
  return mod;
}

// ------------------------------------------------------------------ code generation

const INT_TYPES = new Set(["integer", "long", "byte"]);

class Compiler {
  constructor(modules) {
    this.modules = modules;
    this.globals = new Map(); // public procs: name -> {mod, proc}
    this.label = 0;
    for (const m of modules) {
      m.key = m.name.toLowerCase();
      m.syms = new Map();
      for (const c of m.consts) this.addSym(m, c.name, { kind: "const", type: c.type, node: c, vis: c.vis, mod: m });
      for (const d of m.decls) for (const v of d.vars) {
        this.addSym(m, v.name, { kind: "var", type: v.type, dims: v.dims, isArray: v.dims !== null, vis: d.vis ?? "private", mod: m });
      }
      for (const p of m.procs) {
        this.addSym(m, p.name, { kind: "proc", proc: p, vis: p.vis, mod: m });
        if (p.vis === "public") {
          if (this.globals.has(p.name)) throw new VbaCompileError(`Ambiguous name detected: ${p.name}`);
          this.globals.set(p.name, { kind: "proc", proc: p, vis: "public", mod: m });
        }
      }
    }
    for (const m of modules) {
      // Excel: a worksheet function named like its module evaluates to #NAME?.
      if (this.globals.has(m.key)) throw new VbaCompileError(`module ${m.name} has the same name as public procedure ${m.key}`);
    }
  }
  addSym(m, name, sym) {
    if (m.syms.has(name)) throw new VbaCompileError(`${m.file}: duplicate declaration ${name}`);
    if (BUILTINS[name] || CONSTANTS[name]) throw new VbaCompileError(`${m.file}: ${name} shadows a built-in`);
    m.syms.set(name, sym);
  }
  jsName(sym, name) {
    switch (sym.kind) {
      case "var": case "const": return `${sym.mod.key}$${name}`;
      case "proc": return `${sym.mod.key}$P_${name}`;
      case "local": return `l_${name}`;
      case "param": return `p_${name}`;
    }
    throw new Error(sym.kind);
  }

  resolve(name, ctx) {
    if (ctx.locals.has(name)) return ctx.locals.get(name);
    const ms = ctx.mod.syms.get(name);
    if (ms) return ms;
    const g = this.globals.get(name);
    if (g) return g;
    for (const m of this.modules) {
      const s = m.syms.get(name);
      if (s && s.vis === "public" && s.kind !== "proc") return s;
    }
    return null;
  }

  // ---- expressions: return { c: js code, t: static type }
  ex(e, ctx) {
    switch (e.k) {
      case "lit":
        return { c: e.type === "string" ? JSON.stringify(e.v) : String(e.v), t: e.type };
      case "paren": {
        const a = this.ex(e.a, ctx);
        return { c: `(${a.c})`, t: a.t };
      }
      case "cat": {
        const a = this.ex(e.a, ctx), b = this.ex(e.b, ctx);
        return { c: `(R.toStr(${a.c}) + R.toStr(${b.c}))`, t: "string" };
      }
      case "arith": {
        const a = this.ex(e.a, ctx), b = this.ex(e.b, ctx);
        let kind;
        if (e.op === "/" || e.op === "^") kind = "double";
        else if (INT_TYPES.has(a.t) && INT_TYPES.has(b.t)) kind = a.t === "long" || b.t === "long" ? "long" : "integer";
        else if (e.op === "\\" || e.op === "mod") kind = "long";
        else if (a.t === "string" && b.t === "string" && e.op === "+") kind = "string";
        else kind = a.t === "variant" || b.t === "variant" ? "variant" : "double";
        return { c: `R.arith(${JSON.stringify(e.op)}, ${JSON.stringify(kind)}, ${a.c}, ${b.c})`, t: kind === "byte" ? "integer" : kind };
      }
      case "cmp": {
        const a = this.ex(e.a, ctx), b = this.ex(e.b, ctx);
        return { c: `R.compare(${JSON.stringify(e.op)}, ${a.c}, ${b.c})`, t: "boolean" };
      }
      case "logic": {
        const a = this.ex(e.a, ctx), b = this.ex(e.b, ctx);
        return { c: `R.logic(${JSON.stringify(e.op)}, ${a.c}, ${b.c})`, t: a.t === "boolean" && b.t === "boolean" ? "boolean" : "long" };
      }
      case "not": {
        const a = this.ex(e.a, ctx);
        return { c: `R.not(${a.c})`, t: a.t === "boolean" ? "boolean" : "long" };
      }
      case "neg": {
        const a = this.ex(e.a, ctx);
        return { c: `R.neg(${a.c})`, t: a.t };
      }
      case "name":
        return this.nameRead(e.name, ctx);
      case "call":
        return this.callExpr(e.name, e.args, ctx);
      case "member": {
        const o = this.ex(e.obj, ctx);
        const args = e.args ? `[${e.args.map((a) => this.ex(a, ctx).c).join(", ")}]` : "null";
        return { c: `R.member(${o.c}, ${JSON.stringify(e.name)}, ${args})`, t: "variant" };
      }
    }
    throw new VbaCompileError(`${ctx.where}: expression ${e.k}`);
  }

  nameRead(name, ctx) {
    const s = this.resolve(name, ctx);
    if (!s) {
      if (CONSTANTS[name]) {
        const [v, t] = CONSTANTS[name];
        return { c: v === EMPTY ? "R.EMPTY" : JSON.stringify(v), t };
      }
      if (BUILTINS[name]) return this.callExpr(name, [], ctx);
      throw new VbaCompileError(`${ctx.where}: Variable not defined: ${name}`);
    }
    if (s.kind === "proc") {
      if (s.proc === ctx.proc) throw new VbaCompileError(`${ctx.where}: reading the function name ${name} inside itself is not supported`);
      return this.callExpr(name, [], ctx);
    }
    if (s.isArray) return { c: this.arrRef(s, name), t: "array" };
    if (s.kind === "param" && s.byRef) return { c: `${this.jsName(s, name)}.v`, t: s.type };
    return { c: this.jsName(s, name), t: s.type };
  }

  arrRef(s, name) {
    return s.kind === "param" ? this.jsName(s, name) : `R.arr(${this.jsName(s, name)})`;
  }

  callExpr(name, args, ctx) {
    const s = this.resolve(name, ctx);
    if (s && s.isArray) {
      if (!args.length || args.some((a) => a === null)) throw new VbaCompileError(`${ctx.where}: bad subscript`);
      const idx = args.map((a) => this.ex(a, ctx).c).join(", ");
      return { c: `R.aget(${this.arrRef(s, name)}, [${idx}])`, t: s.type };
    }
    if (s && s.kind === "proc") {
      if (!s.proc.isFunc && ctx.inExpr) throw new VbaCompileError(`${ctx.where}: Sub ${name} used as a function`);
      return { c: this.procCall(s, name, args, ctx), t: s.proc.retType ?? "variant" };
    }
    if (s) throw new VbaCompileError(`${ctx.where}: ${name} is not a function or array`);
    const b = BUILTINS[name];
    if (!b) {
      if (name === "msgbox") return { c: `R.host.msgBox(${args.map((a) => (a ? this.ex(a, ctx).c : "R.MISSING")).join(", ")})`, t: "long" };
      throw new VbaCompileError(`${ctx.where}: Sub or Function not defined: ${name}`);
    }
    if (args.some((a) => a === null)) throw new VbaCompileError(`${ctx.where}: missing argument to ${name}`);
    const cargs = args.map((a) => {
      if ((name === "ubound" || name === "lbound") && a === args[0]) {
        const x = this.ex(a, ctx);
        if (x.t !== "array") throw new VbaCompileError(`${ctx.where}: ${name} needs an array`);
        return x.c;
      }
      return this.ex(a, ctx).c;
    });
    return { c: `R.B.${name}(${cargs.join(", ")})`, t: b.type };
  }

  procCall(s, name, args, ctx) {
    const p = s.proc;
    if (args.length > p.params.length) throw new VbaCompileError(`${ctx.where}: too many arguments to ${name}`);
    const out = p.params.map((prm, i) => {
      const a = args[i] ?? null;
      if (a === null) {
        if (!prm.optional) throw new VbaCompileError(`${ctx.where}: Argument not optional (${name}.${prm.name})`);
        return "R.MISSING";
      }
      if (prm.isArray) {
        // Arrays are always passed by reference; a dynamic array may still be undimensioned
        // (the callee can ReDim it), so pass the variable itself.
        const vs = a.k === "name" ? this.resolve(a.name, ctx) : null;
        if (!vs || !vs.isArray) throw new VbaCompileError(`${ctx.where}: array variable expected for ${prm.name}`);
        if (prm.type !== "variant" && vs.type !== prm.type) throw new VbaCompileError(`${ctx.where}: ByRef argument type mismatch (${a.name}() -> ${name}.${prm.name})`);
        return this.jsName(vs, a.name);
      }
      if (!prm.byRef) return this.ex(a, ctx).c;
      // ByRef
      if (a.k === "name") {
        const vs = this.resolve(a.name, ctx);
        if (vs && (vs.kind === "local" || vs.kind === "param" || vs.kind === "var") && !vs.isArray) {
          if (prm.type !== "variant" && vs.type !== prm.type) {
            throw new VbaCompileError(`${ctx.where}: ByRef argument type mismatch (${a.name} As ${vs.type} -> ${name}.${prm.name} As ${prm.type})`);
          }
          if (vs.kind === "param" && vs.byRef) return this.jsName(vs, a.name);
          const js = this.jsName(vs, a.name);
          return `{ get v() { return ${js}; }, set v(x) { ${js} = R.coerce(${JSON.stringify(vs.type)}, x); } }`;
        }
      }
      if (a.k === "call") {
        const vs = this.resolve(a.name, ctx);
        if (vs && vs.isArray) {
          if (prm.type !== "variant" && vs.type !== prm.type) throw new VbaCompileError(`${ctx.where}: ByRef argument type mismatch (${a.name}() -> ${prm.name})`);
          const arr = this.arrRef(vs, a.name);
          const idx = a.args.map((x) => this.ex(x, ctx).c).join(", ");
          return `R.elemRef(${arr}, [${idx}])`;
        }
      }
      return `{ v: ${this.ex(a, ctx).c} }`;
    });
    return `${this.jsName(s, name)}(${out.join(", ")})`;
  }

  // ---- statements
  block(list, ctx, ind) {
    return list.map((s) => this.stmt(s, ctx, ind)).join("\n");
  }

  stmt(s, ctx, ind) {
    ctx.where = s.where ?? ctx.where;
    const I = ind;
    const cond = (e) => `R.toBool(${this.ex(e, ctx).c})`;
    switch (s.k) {
      case "dim": case "const": return `${I}/* ${s.k} */`;
      case "assign": return I + this.assign(s.target, s.value, ctx) + ";";
      case "callstmt": {
        const sym = this.resolve(s.name, ctx);
        if (!sym && s.name === "msgbox") return `${I}R.host.msgBox(${s.args.map((a) => (a ? this.ex(a, ctx).c : "R.MISSING")).join(", ")});`;
        if (!sym || sym.kind !== "proc") throw new VbaCompileError(`${ctx.where}: Sub or Function not defined: ${s.name}`);
        return `${I}${this.procCall(sym, s.name, s.args, ctx)};`;
      }
      case "print":
        return `${I}R.host.print(${s.parts.map((p) => `R.toStr(${this.ex(p, ctx).c})`).join(" + ") || '""'});`;
      case "redim": {
        return s.items.map((it) => {
          const sym = this.resolve(it.name, ctx);
          if (!sym || !sym.isArray || sym.fixed) throw new VbaCompileError(`${ctx.where}: ReDim of a non-dynamic array ${it.name}`);
          const dims = it.dims.map(([a, b]) => `[R.toLong(${this.ex(a, ctx).c}), R.toLong(${this.ex(b, ctx).c})]`).join(", ");
          return `${I}R.redim(${this.jsName(sym, it.name)}, [${dims}], ${s.preserve});`;
        }).join("\n");
      }
      case "ifline": {
        const t = this.block(s.thenS, ctx, I + "  ");
        const e = s.elseS.length ? ` else {\n${this.block(s.elseS, ctx, I + "  ")}\n${I}}` : "";
        return `${I}if (${cond(s.cond)}) {\n${t}\n${I}}${e}`;
      }
      case "if": {
        let out = "";
        s.arms.forEach((arm, i) => {
          out += `${i === 0 ? I + "if" : " else if"} (${cond(arm.cond)}) {\n${this.block(arm.body, ctx, I + "  ")}\n${I}}`;
        });
        if (s.elseBody) out += ` else {\n${this.block(s.elseBody, ctx, I + "  ")}\n${I}}`;
        return out;
      }
      case "select": {
        const v = `__sel${++this.label}`;
        let out = `${I}{\n${I}  const ${v} = ${this.ex(s.expr, ctx).c};\n`;
        let first = true;
        let hasElse = false;
        for (const c of s.cases) {
          if (hasElse) throw new VbaCompileError(`${ctx.where}: Case after Case Else`);
          let test;
          if (c.items === null) {
            hasElse = true;
            test = null;
          } else {
            test = c.items.map((it) => {
              if (it.is) return `R.compare(${JSON.stringify(it.is)}, ${v}, ${this.ex(it.v, ctx).c})`;
              if (it.from) return `(R.compare(">=", ${v}, ${this.ex(it.from, ctx).c}) && R.compare("<=", ${v}, ${this.ex(it.to, ctx).c}))`;
              return `R.compare("=", ${v}, ${this.ex(it.v, ctx).c})`;
            }).join(" || ");
          }
          const body = this.block(c.body, ctx, I + "    ");
          if (test === null) out += first ? `${I}  {\n${body}\n${I}  }\n` : ` else {\n${body}\n${I}  }\n`;
          else out += `${first ? I + "  if" : " else if"} (${test}) {\n${body}\n${I}  }`;
          first = false;
        }
        return out + `\n${I}}`;
      }
      case "for": {
        const sym = this.resolve(s.v, ctx);
        if (!sym || (sym.kind !== "local" && sym.kind !== "param") || sym.isArray || !["long", "integer", "double"].includes(sym.type)) {
          throw new VbaCompileError(`${ctx.where}: For variable ${s.v} must be a numeric local`);
        }
        const lbl = `L${++this.label}`;
        const vv = sym.kind === "param" && sym.byRef ? `${this.jsName(sym, s.v)}.v` : this.jsName(sym, s.v);
        const T = JSON.stringify(sym.type);
        ctx.loops.push({ kind: "for", lbl });
        const body = this.block(s.body, ctx, I + "    ");
        ctx.loops.pop();
        return `${I}{\n${I}  const ${lbl}e = R.num(${this.ex(s.to, ctx).c}), ${lbl}s = ${s.step ? `R.num(${this.ex(s.step, ctx).c})` : "1"};\n` +
          `${I}  ${vv} = R.coerce(${T}, ${this.ex(s.from, ctx).c});\n` +
          `${I}  ${lbl}: for (; ${lbl}s >= 0 ? ${vv} <= ${lbl}e : ${vv} >= ${lbl}e; ${vv} = R.coerce(${T}, ${vv} + ${lbl}s)) {\n${body}\n${I}  }\n${I}}`;
      }
      case "do": {
        const lbl = `L${++this.label}`;
        ctx.loops.push({ kind: "do", lbl });
        const body = this.block(s.body, ctx, I + "  ");
        ctx.loops.pop();
        const test = (c) => (c.until ? `${cond(c.cond)}` : `!${cond(c.cond)}`);
        const pre = s.pre ? `${I}  if (${test(s.pre)}) break;\n` : "";
        const post = s.post ? `\n${I}  if (${test(s.post)}) break;` : "";
        return `${I}${lbl}: while (true) {\n${pre}${body}${post}\n${I}}`;
      }
      case "exit": {
        if (s.what === "function" || s.what === "sub") {
          if ((s.what === "function") !== ctx.proc.isFunc) throw new VbaCompileError(`${ctx.where}: Exit ${s.what} in the wrong procedure kind`);
          return `${I}return __ret;`;
        }
        for (let i = ctx.loops.length - 1; i >= 0; i--) {
          if (ctx.loops[i].kind === s.what) return `${I}break ${ctx.loops[i].lbl};`;
        }
        throw new VbaCompileError(`${ctx.where}: Exit ${s.what} outside a loop`);
      }
    }
    throw new VbaCompileError(`${ctx.where}: statement ${s.k}`);
  }

  assign(target, value, ctx) {
    const v = this.ex(value, ctx);
    if (v.t === "array") throw new VbaCompileError(`${ctx.where}: array assignment not supported`);
    if (target.k === "name") {
      if (ctx.proc.isFunc && target.name === ctx.proc.name) return `__ret = R.coerce(${JSON.stringify(ctx.proc.retType)}, ${v.c})`;
      const s = this.resolve(target.name, ctx);
      if (!s) throw new VbaCompileError(`${ctx.where}: Variable not defined: ${target.name}`);
      if (s.kind === "const") throw new VbaCompileError(`${ctx.where}: assignment to constant ${target.name}`);
      if (s.kind === "proc") throw new VbaCompileError(`${ctx.where}: assignment to procedure ${target.name}`);
      if (s.isArray) throw new VbaCompileError(`${ctx.where}: assignment to array ${target.name}`);
      const js = s.kind === "param" && s.byRef ? `${this.jsName(s, target.name)}.v` : this.jsName(s, target.name);
      return `${js} = R.coerce(${JSON.stringify(s.type)}, ${v.c})`;
    }
    const s = this.resolve(target.name, ctx);
    if (!s || !s.isArray) throw new VbaCompileError(`${ctx.where}: ${target.name} is not an array`);
    const idx = target.args.map((a) => this.ex(a, ctx).c).join(", ");
    return `R.aset(${this.arrRef(s, target.name)}, [${idx}], ${v.c})`;
  }

  collectDims(list, out) {
    for (const s of list) {
      if (s.k === "dim" || s.k === "const") out.push(s);
      if (s.k === "if") { s.arms.forEach((a) => this.collectDims(a.body, out)); if (s.elseBody) this.collectDims(s.elseBody, out); }
      if (s.k === "select") s.cases.forEach((c) => this.collectDims(c.body, out));
      if (s.k === "for" || s.k === "do") this.collectDims(s.body, out);
    }
    return out;
  }

  constValue(node, ctx) {
    return `R.coerce(${JSON.stringify(node.type)}, ${this.ex(node.value, ctx).c})`;
  }

  proc(m, p) {
    const ctx = { mod: m, proc: p, locals: new Map(), loops: [], where: p.where, inExpr: true };
    for (const prm of p.params) {
      if (ctx.locals.has(prm.name)) throw new VbaCompileError(`${p.where}: duplicate parameter ${prm.name}`);
      ctx.locals.set(prm.name, { kind: "param", type: prm.type, byRef: prm.byRef && !prm.isArray, isArray: prm.isArray });
    }
    const pre = [];
    for (const prm of p.params) {
      const js = `p_${prm.name}`;
      if (prm.optional) {
        if (prm.isArray) throw new VbaCompileError(`${p.where}: optional array parameter`);
        if (prm.def) {
          const d = this.ex(prm.def, { ...ctx, locals: new Map() });
          pre.push(`if (${js} === R.MISSING) ${js} = ${prm.byRef ? `{ v: R.coerce(${JSON.stringify(prm.type)}, ${d.c}) }` : d.c};`);
        } else if (prm.type !== "variant") {
          pre.push(`if (${js} === R.MISSING) ${js} = ${prm.byRef ? `{ v: R.default(${JSON.stringify(prm.type)}) }` : `R.default(${JSON.stringify(prm.type)})`};`);
        } else if (prm.byRef) {
          pre.push(`if (${js} === R.MISSING) ${js} = { v: R.MISSING };`);
        }
      }
      if (!prm.byRef && !prm.isArray) pre.push(`${js} = R.coerce(${JSON.stringify(prm.type)}, ${js});`);
    }
    for (const d of this.collectDims(p.body, [])) {
      if (d.k === "const") {
        if (ctx.locals.has(d.name)) throw new VbaCompileError(`${p.where}: duplicate ${d.name}`);
        ctx.locals.set(d.name, { kind: "local", type: d.type, isConst: true });
        pre.push(`const l_${d.name} = ${this.constValue(d, ctx)};`);
        continue;
      }
      if (d.vis) throw new VbaCompileError(`${p.where}: Private/Public inside a procedure`);
      for (const v of d.vars) {
        if (ctx.locals.has(v.name)) throw new VbaCompileError(`${p.where}: Duplicate declaration in current scope: ${v.name}`);
        if (m.syms.get(v.name)?.kind === "proc" || this.globals.has(v.name)) {
          throw new VbaCompileError(`${p.where}: local ${v.name} shadows a procedure (confusing in VBA)`);
        }
        const isArray = v.dims !== null;
        ctx.locals.set(v.name, { kind: "local", type: v.type, isArray, fixed: isArray && v.dims.length > 0 });
        if (!isArray) pre.push(`let l_${v.name} = R.default(${JSON.stringify(v.type)});`);
        else if (v.dims.length === 0) pre.push(`let l_${v.name} = R.dyn(${JSON.stringify(v.type)});`);
        else pre.push(`let l_${v.name} = R.fixed(${JSON.stringify(v.type)}, [${v.dims.map(([a, b]) => `[${this.ex(a, ctx).c}, ${this.ex(b, ctx).c}]`).join(", ")}]);`);
      }
    }
    for (const [name, sym] of ctx.locals) {
      if (sym.kind === "param" && (m.syms.get(name)?.kind === "proc" || this.globals.has(name))) {
        throw new VbaCompileError(`${p.where}: parameter ${name} shadows a procedure`);
      }
    }
    const ret = p.isFunc ? `R.default(${JSON.stringify(p.retType)})` : "undefined";
    const body = this.block(p.body, ctx, "  ");
    const params = p.params.map((x) => `p_${x.name}`).join(", ");
    return `function ${m.key}$P_${p.name}(${params}) {\n  ${pre.join("\n  ")}\n  let __ret = ${ret};\n${body}\n  return __ret;\n}`;
  }

  compile() {
    const parts = [];
    for (const m of this.modules) {
      const mctx = { mod: m, proc: null, locals: new Map(), loops: [], where: m.file };
      for (const c of m.consts) parts.push(`const ${m.key}$${c.name} = ${this.constValue(c, mctx)};`);
      for (const d of m.decls) for (const v of d.vars) {
        const js = `${m.key}$${v.name}`;
        const sym = m.syms.get(v.name);
        if (v.dims === null) parts.push(`let ${js} = R.default(${JSON.stringify(v.type)});`);
        else if (v.dims.length === 0) parts.push(`let ${js} = R.dyn(${JSON.stringify(v.type)});`);
        else {
          sym.fixed = true;
          parts.push(`let ${js} = R.fixed(${JSON.stringify(v.type)}, [${v.dims.map(([a, b]) => `[${this.ex(a, mctx).c}, ${this.ex(b, mctx).c}]`).join(", ")}]);`);
        }
      }
    }
    for (const m of this.modules) for (const p of m.procs) parts.push(this.proc(m, p));
    const exportsList = [];
    for (const [name, s] of this.globals) exportsList.push(`${JSON.stringify(name)}: { fn: ${s.mod.key}$P_${name}, proc: P[${JSON.stringify(name)}] }`);
    return `"use strict";\n${parts.join("\n\n")}\nreturn { ${exportsList.join(",\n")} };`;
  }
}

// ------------------------------------------------------------------ runtime object

function makeRuntime(host) {
  return {
    EMPTY, MISSING, B: Object.fromEntries(Object.entries(BUILTINS).map(([k, v]) => [k, v.f])),
    host,
    toStr, toBool, toLong, num, coerce, compare, arith, logic, not, neg, member,
    default: defaultValue,
    dyn: (type) => new DynArray(type),
    fixed: (type, dims) => new VArray(type, dims.map(([a, b]) => [toLong(a), toLong(b)])),
    arr(a) {
      if (a instanceof DynArray) {
        if (!a.arr) fail(9, "Subscript out of range (array not dimensioned)");
        return a.arr;
      }
      return a;
    },
    redim(d, dims, preserve) {
      if (!(d instanceof DynArray)) fail(10, "This array is fixed or temporarily locked");
      const n = new VArray(d.type, dims);
      if (preserve && d.arr) {
        if (dims.length !== 1 || d.arr.bounds.length !== 1 || d.arr.bounds[0][0] !== dims[0][0]) unsupported("ReDim Preserve beyond the last dimension");
        for (let i = 0; i < Math.min(n.data.length, d.arr.data.length); i++) n.data[i] = d.arr.data[i];
      }
      d.arr = n;
    },
    aget(a, idx) {
      if (a instanceof DynArray) a = this.arr(a);
      return a.data[a.offset(idx)];
    },
    aset(a, idx, v) {
      if (a instanceof DynArray) a = this.arr(a);
      a.data[a.offset(idx)] = coerce(a.type, v);
    },
    elemRef(a, idx) {
      if (a instanceof DynArray) a = this.arr(a);
      const off = a.offset(idx);
      return { get v() { return a.data[off]; }, set v(x) { a.data[off] = coerce(a.type, x); } };
    },
  };
}

/**
 * Compile VBA modules. `sources`: [{ file, text }].
 * Returns { call(name, ...args), output: string[] (Debug.Print lines), messages: MsgBox prompts }.
 */
export function loadVba(sources, { msgBox } = {}) {
  const modules = sources.map(({ file, text }) => parseModule(text, file));
  const compiler = new Compiler(modules);
  const js = compiler.compile();
  const output = [];
  const messages = [];
  const host = {
    print: (s) => output.push(s),
    msgBox: (prompt, buttons, title) => {
      messages.push({ prompt: toStr(prompt), buttons: buttons === undefined || buttons === MISSING ? 0 : toLong(buttons), title: title === undefined || title === MISSING ? "" : toStr(title) });
      return msgBox ? msgBox(prompt) : 1;
    },
  };
  const R = makeRuntime(host);
  const P = {};
  for (const [name, s] of compiler.globals) P[name] = s.proc;
  const exportsObj = new Function("R", "P", js)(R, P);
  return {
    js,
    output,
    messages,
    procedures: Object.keys(exportsObj),
    /** Call a public procedure. Arguments are passed as ByVal values (or wrapped for ByRef). */
    call(name, ...args) {
      const e = exportsObj[name.toLowerCase()];
      if (!e) throw new Error(`no public procedure ${name}`);
      const params = e.proc.params;
      if (args.length > params.length) throw new Error(`too many arguments for ${name}`);
      const full = params.map((p, i) => {
        const a = i < args.length && args[i] !== undefined ? args[i] : MISSING;
        if (a === MISSING && !p.optional) throw new Error(`argument ${p.name} of ${name} is required`);
        return p.byRef && a !== MISSING ? { v: a } : a;
      });
      return e.fn(...full);
    },
  };
}
