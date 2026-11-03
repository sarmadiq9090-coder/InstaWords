// Generates vba/Tafqit.bas and vba/TafqitTests.bas.
//
//   vba/Tafqit.template.bas  (hand-written logic, placeholders '@@NAME@@)
// + src/lexicon.ts, src/lang/en.ts, src/lang/fr.ts (number words of each language)
// + src/errors.ts           (error codes and messages)
// + data/units.json         (unit catalog, validated with src/catalog.ts)
// + tests/golden/cases.json (golden cases for TafqitSelfTest)
//
// VBA cannot read JSON offline and its editor is code-page based, so every
// string is embedded as code: printable ASCII as a literal, anything else as
// U("hex code units") decoded at run time. Output is pure ASCII with CRLF.
//
// Usage: node scripts/build-vba.mjs [--units <units.json>] [--out <dir>]
// (the options are for tests that build the module from a custom catalog).
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { createHash } from "node:crypto";
import { resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { Catalog, unitKey } from "../src/catalog.ts";
import { ERROR_MESSAGES } from "../src/errors.ts";
import * as LX from "../src/lexicon.ts";
import { LANGUAGE_MODULES } from "../src/lang/index.ts";
import { EN_WORDS } from "../src/lang/en.ts";
import { FR_WORDS } from "../src/lang/fr.ts";

const root = fileURLToPath(new URL("..", import.meta.url));
const read = (p) => readFileSync(root + p, "utf8");

const UNITS_PER_PROC = 6; // keeps each generated procedure far below VBA's 64 KB limit
const CASES_PER_PROC = 25;
const CHUNK = 100; // code units per U("...") call, keeps lines < 1023 chars

// ---------------------------------------------------------------- encoding

const isPrintableAscii = (s) => /^[\x20-\x7e]*$/.test(s);
const hex = (s) => Array.from({ length: s.length }, (_, i) => s.charCodeAt(i).toString(16).toUpperCase().padStart(4, "0")).join(" ");

/** VBA expressions for a string, split into chunks (joined with &). */
function vbaPieces(s) {
  if (isPrintableAscii(s)) return [`"${s.replace(/"/g, '""')}"`];
  const out = [];
  for (let i = 0; i < s.length; i += CHUNK) out.push(`U("${hex(s.slice(i, i + CHUNK))}")`);
  return out;
}

const TRANSLIT = {
  "ا": "a", "أ": "a", "إ": "i", "آ": "aa", "ٱ": "a", "ب": "b", "ت": "t", "ث": "th", "ج": "j", "ح": "h",
  "خ": "kh", "د": "d", "ذ": "dh", "ر": "r", "ز": "z", "س": "s", "ش": "sh", "ص": "s", "ض": "d", "ط": "t",
  "ظ": "z", "ع": "'", "غ": "gh", "ف": "f", "ق": "q", "ك": "k", "ل": "l", "م": "m", "ن": "n", "ه": "h",
  "و": "w", "ؤ": "u'", "ي": "y", "ى": "a", "ئ": "i'", "ء": "'", "ة": "a", "ً": "n", "ٌ": "un", "ٍ": "in",
  "َ": "", "ُ": "", "ِ": "", "ّ": "", "ْ": "", "ـ": "", "پ": "p", "چ": "ch", "ڤ": "v", "گ": "g", "ک": "k",
  "ی": "y", "٫": ".", "٬": ",", "،": ",", "؟": "?",
};
/** ASCII-only transliteration for comments (the VBA editor cannot show Arabic in a .bas). */
function translit(s) {
  let out = "";
  const latin = s.normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/œ/g, "oe").replace(/æ/g, "ae").replace(/ß/g, "ss");
  for (const ch of latin) {
    const c = ch.codePointAt(0);
    if (TRANSLIT[ch] !== undefined) out += TRANSLIT[ch];
    else if (c >= 0x660 && c <= 0x669) out += String(c - 0x660);
    else if (c >= 0x6f0 && c <= 0x6f9) out += String(c - 0x6f0);
    else if (c >= 0x20 && c < 0x7f) out += ch;
    else out += "?";
  }
  return out;
}

/** Statements assigning string `s` to `target`, with an ASCII comment. */
function assign(target, s, indent = "    ") {
  const pieces = vbaPieces(s);
  const note = isPrintableAscii(s) ? "" : ` ' ${translit(s)}`;
  if (pieces.length === 1) return [`${indent}${target} = ${pieces[0]}${note}`];
  return pieces.map((p, i) => (i === 0 ? `${indent}${target} = ${p}${note}` : `${indent}${target} = ${target} & ${p}`));
}

// ---------------------------------------------------------------- shared inputs

const template = read("vba/Tafqit.template.bas");

const hundredsNames = Object.keys(LX.HUNDREDS);
if (hundredsNames.length !== 2 || hundredsNames[0] !== "مائة") throw new Error("expected HUNDREDS = {مائة, مئة}");
for (const k of [1, 2, 3, 4]) if (!LX.SCALES[k]) throw new Error(`SCALES[${k}] missing`);
if (LX.SCALES.length !== 5) throw new Error("expected 4 scales");
if (LX.MAX_INTEGER_DIGITS !== 15) throw new Error("MAX_INTEGER_DIGITS changed: check scales and NumberText");

// The VBA rules are written for these languages, in this order (LANG_AR, LANG_EN, LANG_FR).
const LANGS = ["ar", "en", "fr"];
if (Object.keys(LANGUAGE_MODULES).join() !== LANGS.join()) throw new Error(`languages changed: ${Object.keys(LANGUAGE_MODULES)}`);
const expectLen = (name, a, n) => { if (a.length !== n) throw new Error(`${name} has ${a.length} entries, the VBA module expects ${n}`); };
expectLen("EN_WORDS.SMALL", EN_WORDS.SMALL, 20);
expectLen("EN_WORDS.TENS", EN_WORDS.TENS, 10);
expectLen("EN_WORDS.SCALES", EN_WORDS.SCALES, 5);
expectLen("FR_WORDS.SMALL", FR_WORDS.SMALL, 20);
expectLen("FR_WORDS.TENS", FR_WORDS.TENS, 7);
expectLen("FR_WORDS.SCALE_NOUNS", FR_WORDS.SCALE_NOUNS, 5);

/** Error codes in src/errors.ts order; E_<CODE> = 1, 2, ... */
const ERROR_CODES = Object.keys(ERROR_MESSAGES);

// ---------------------------------------------------------------- JS character classes

// /\s/ (and String.prototype.trim) over the BMP, as code ranges.
function ranges(pred) {
  const out = [];
  for (let c = 0; c <= 0xffff; c++) {
    if (!pred(c)) continue;
    const last = out[out.length - 1];
    if (last && last[1] === c - 1) last[1] = c;
    else out.push([c, c]);
  }
  return out;
}
const spaceRanges = ranges((c) => /\s/.test(String.fromCharCode(c)));
for (let c = 0; c <= 0xffff; c++) {
  const ch = String.fromCharCode(c);
  if (/\s/.test(ch) !== (ch.trim() === "")) throw new Error(`trim and \\s disagree at U+${c.toString(16)}`);
}

/** Lookup names of a unit, as Catalog.add builds them. */
function unitKeysOf(u) {
  const idKey = unitKey(u.id);
  const names = new Set([u.label, u.code ?? "", ...(u.aliases ?? [])].map(unitKey).filter(Boolean));
  names.delete(idKey);
  return [idKey, ...names];
}

// ---------------------------------------------------------------- Tafqit.bas

function lexiconProc() {
  const L = ["Private Sub InitLexicon()"];
  L.push(...assign("mZero", LX.ZERO), ...assign("mWaw", LX.WAW), ...assign("mPoint", LX.DECIMAL_POINT_WORD));
  ["m", "f"].forEach((g, gi) => {
    LX.ONES[g].forEach((w, i) => w && L.push(...assign(`mOnes(${gi}, ${i})`, w)));
    LX.TEENS[g].forEach((w, i) => w && L.push(...assign(`mTeens(${gi}, ${i})`, w)));
    LX.COMPOUND_ONES[g].forEach((w, i) => w && L.push(...assign(`mComp(${gi}, ${i})`, w)));
  });
  LX.TENS.forEach((w, i) => w && L.push(...assign(`mTens(${i})`, w)));
  hundredsNames.forEach((name, hf) => {
    const t = LX.HUNDREDS[name];
    L.push(...assign(`mHfName(${hf})`, name));
    t.words.forEach((w, i) => w && L.push(...assign(`mHund(${hf}, ${i})`, w)));
    L.push(...assign(`mHundDC(${hf})`, t.dualConstruct));
  });
  for (let k = 1; k <= 4; k++) {
    const s = LX.SCALES[k];
    L.push(...assign(`mScale(${k}, SC_SINGULAR)`, s.singular), ...assign(`mScale(${k}, SC_DUAL)`, s.dual));
    L.push(...assign(`mScale(${k}, SC_PLURAL)`, s.plural), ...assign(`mScale(${k}, SC_ACCUSATIVE)`, s.accusative));
  }
  for (const code of ERROR_CODES) L.push(...assign(`mErrMsg(E_${code})`, ERROR_MESSAGES[code]));
  LANGS.forEach((id, i) => L.push(...assign(`mLangId(${i})`, id)));
  L.push(...assign("mJoiner(LANG_AR)", LX.WAW), ...assign("mJoiner(LANG_EN)", EN_WORDS.SUBUNIT_JOINER));
  L.push(...assign("mJoiner(LANG_FR)", FR_WORDS.SUBUNIT_JOINER));
  L.push("End Sub");
  return L;
}

function englishProc() {
  const W = EN_WORDS;
  const L = ["Private Sub InitWordsEn()"];
  W.SMALL.forEach((w, i) => L.push(...assign(`mEnSmall(${i})`, w)));
  W.TENS.forEach((w, i) => w && L.push(...assign(`mEnTens(${i})`, w)));
  W.SCALES.forEach((w, i) => w && L.push(...assign(`mEnScale(${i})`, w)));
  L.push(...assign("mEnHundred", W.HUNDRED), ...assign("mEnPoint", W.POINT), ...assign("mEnHyphen", W.HYPHEN));
  L.push("End Sub");
  return L;
}

function frenchProc() {
  const W = FR_WORDS;
  const L = ["Private Sub InitWordsFr()"];
  W.SMALL.forEach((w, i) => L.push(...assign(`mFrSmall(${i})`, w)));
  W.TENS.forEach((w, i) => w && L.push(...assign(`mFrTens(${i})`, w)));
  W.SCALE_NOUNS.forEach((w, i) => w && L.push(...assign(`mFrScale(${i})`, w)));
  for (const [v, k] of [["mFrFourTwenty", "FOUR_TWENTY"], ["mFrCent", "CENT"], ["mFrMille", "MILLE"], ["mFrPoint", "POINT"],
    ["mFrEt", "ET"], ["mFrHyphen", "HYPHEN"], ["mFrPluralS", "PLURAL_S"], ["mFrOneF", "ONE_F"], ["mFrDe", "DE"], ["mFrDeElided", "DE_ELIDED"]]) {
    if (typeof W[k] !== "string") throw new Error(`FR_WORDS.${k} missing`);
    L.push(...assign(v, W[k]));
  }
  L.push("End Sub");
  return L;
}

/** FR_WORDS.ELIDES over every UTF-16 code unit (what the regex sees for the first character). */
function frenchElisionProc() {
  const cases = ranges((c) => FR_WORDS.ELIDES.test(String.fromCharCode(c))).map(([a, b]) => (a === b ? `${a}` : `${a} To ${b}`));
  const L = ["' A French noun starting with this code unit takes d' (FR_WORDS.ELIDES in src/lang/fr.ts).",
    "Private Function FrElides(ByVal c As Long) As Boolean", "    Select Case c"];
  for (let i = 0; i < cases.length; i += 8) L.push(`    Case ${cases.slice(i, i + 8).join(", ")}`, "        FrElides = True");
  L.push("    Case Else", "        FrElides = False", "    End Select", "End Function");
  return L;
}

/** Forms of a unit or subunit in each language: [lang index, {singular, plural, one?, gender?}]. */
function formsByLang(arabic, i18n) {
  const out = [];
  if (arabic) out.push([0, arabic]);
  if (i18n?.en) out.push([1, i18n.en]);
  if (i18n?.fr) out.push([2, i18n.fr]);
  return out;
}

function formLines(prefix, arabic, i18n) {
  const L = [];
  for (const [li, f] of formsByLang(arabic, i18n)) {
    L.push(`    m${prefix}Has(i, ${li}) = True`);
    if (f.gender !== undefined) L.push(`    m${prefix}Gender(i, ${li}) = ${f.gender === "f" ? 1 : 0}`);
    L.push(...assign(`m${prefix}Sing(i, ${li})`, f.singular), ...assign(`m${prefix}Plur(i, ${li})`, f.plural));
    if (f.one !== undefined) L.push(...assign(`m${prefix}One(i, ${li})`, f.one));
    if (li === 0) L.push(...assign(`m${prefix}Dual(i)`, f.dual), ...assign(`m${prefix}Acc(i)`, f.accusative));
  }
  return L;
}

function unitLines(u, i) {
  const L = [`    ' --- ${u.id}: ${translit(u.label)} ---`, `    i = ${i}`];
  L.push(...assign("mUId(i)", u.id));
  if (u.forms) L.push("    mUHasForms(i) = True", ...formLines("F", u.forms, u.i18n));
  if (u.subunit) {
    L.push("    mUHasSub(i) = True", `    mUSubDigits(i) = ${String(u.subunit.ratio).length - 1}`);
    L.push(...formLines("S", u.subunit, u.subunit.i18n));
  }
  for (const k of unitKeysOf(u)) {
    const pieces = vbaPieces(k);
    if (pieces.length === 1) L.push(`    AddKey ${pieces[0]}, i${isPrintableAscii(k) ? "" : ` ' ${translit(k)}`}`);
    else L.push(...assign("s", k), "    AddKey s, i");
  }
  return L;
}

function catalogProcs(units, keys) {
  const keyCount = keys.reduce((n, k) => n + k.length, 0);
  const parts = [];
  for (let p = 0; p * UNITS_PER_PROC < units.length; p++) {
    const L = [`Private Sub InitCatalog${p + 1}()`, "    Dim i As Long, s As String"];
    units.slice(p * UNITS_PER_PROC, (p + 1) * UNITS_PER_PROC).forEach((u, j) => L.push(...unitLines(u, p * UNITS_PER_PROC + j + 1)));
    L.push("End Sub");
    parts.push(L);
  }
  const init = [
    "Private Sub InitCatalog()",
    ...["mUId", "mUHasForms", "mFDual", "mFAcc", "mUHasSub", "mUSubDigits", "mSDual", "mSAcc"].map((a) => `    ReDim ${a}(1 To UNIT_COUNT)`),
    ...["mFHas", "mFSing", "mFPlur", "mFOne", "mFGender", "mSHas", "mSSing", "mSPlur", "mSOne", "mSGender"]
      .map((a) => `    ReDim ${a}(1 To UNIT_COUNT, 0 To LANG_COUNT - 1)`),
    "    ReDim mKeyName(1 To KEY_COUNT)",
    "    ReDim mKeyUnit(1 To KEY_COUNT)",
    "    mKeyCount = 0",
    ...parts.map((_, p) => `    InitCatalog${p + 1}`),
    "End Sub",
    "",
    "Private Sub AddKey(ByVal key As String, ByVal ui As Long)",
    "    mKeyCount = mKeyCount + 1",
    "    mKeyName(mKeyCount) = key",
    "    mKeyUnit(mKeyCount) = ui",
    "End Sub",
  ];
  return { keyCount, lines: [init, ...parts] };
}

function charProcs(lowerExtras) {
  const cases = spaceRanges.map(([a, b]) => (a === b ? `${a}` : `${a} To ${b}`));
  const space = ["' /\\s/ in JavaScript (also what String.prototype.trim removes).", "Private Function IsJsSpace(ByVal c As Long) As Boolean"];
  space.push("    Select Case c");
  for (let i = 0; i < cases.length; i += 8) space.push(`    Case ${cases.slice(i, i + 8).join(", ")}`, "        IsJsSpace = True");
  space.push("    Case Else", "        IsJsSpace = False", "    End Select", "End Function");

  const lower = [
    "' toLowerCase() of one character: A-Z, plus the characters whose lower case occurs in a catalog key.",
    "Private Function LowerChar(ByVal ch As String) As String",
    "    Dim c As Long",
    "    c = CharCode(ch)",
    "    Select Case c",
    "    Case 65 To 90",
    "        LowerChar = ChrW(c + 32)",
  ];
  for (const [c, lo] of lowerExtras) lower.push(`    Case ${c}`, ...assign("LowerChar", lo, "        "));
  lower.push("    Case Else", "        LowerChar = ch", "    End Select", "End Function");
  return [space, lower];
}

function fill(tpl, values) {
  let out = tpl;
  for (const [name, text] of Object.entries(values)) {
    const marker = `'@@${name}@@`;
    if (!out.includes(marker)) throw new Error(`template: missing ${marker}`);
    out = out.replace(marker, () => text);
  }
  const left = out.match(/'@@[A-Z_]+@@/);
  if (left) throw new Error(`template: unfilled ${left[0]}`);
  return out;
}

// ---------------------------------------------------------------- TafqitTests.bas

function testsModule(golden) {
  const d = golden.defaults ?? {};
  const cases = golden.cases.map((c) => ({ ...d, ...c.options, id: c.id, input: c.input, unit: c.unit, expected: c.expected, cleaned: c.cleaned }));
  const procs = [];
  for (let p = 0; p * CASES_PER_PROC < cases.length; p++) {
    const L = [`Private Sub LoadCases${p + 1}()`];
    for (const c of cases.slice(p * CASES_PER_PROC, (p + 1) * CASES_PER_PROC)) {
      L.push("    mCount = mCount + 1");
      L.push(...assign("mCaseId(mCount)", c.id), ...assign("mCaseIn(mCount)", c.input), ...assign("mCaseUnit(mCount)", c.unit));
      L.push(...assign("mCaseHf(mCount)", c.hundredsForm ?? ""), ...assign("mCasePre(mCount)", c.prefix ?? ""));
      L.push(...assign("mCaseSuf(mCount)", c.suffix ?? ""), ...assign("mCaseDec(mCount)", c.decimals ?? ""));
      L.push(...assign("mCaseLang(mCount)", c.language ?? ""));
      L.push(...assign("mCaseExp(mCount)", c.expected), ...assign("mCaseClean(mCount)", c.cleaned));
    }
    L.push("End Sub");
    procs.push(L.join("\n"));
  }
  return `Attribute VB_Name = "TafqitTests"
' GENERATED by scripts/build-vba.mjs from tests/golden/cases.json (${cases.length} cases).
' Do not edit. Requires the TafqitLib module (vba/Tafqit.bas).
'
' Run TafqitSelfTest (Alt+F8, or F5 in the VBA editor): it compares TAFQIT and
' TAFQIT_CLEAN with every golden case, prints failures to the Immediate window
' (Ctrl+G) as code points, and shows a summary in a message box.
Option Explicit

Private mCount As Long
Private mCaseId() As String
Private mCaseIn() As String
Private mCaseUnit() As String
Private mCaseHf() As String
Private mCasePre() As String
Private mCaseSuf() As String
Private mCaseDec() As String
Private mCaseLang() As String
Private mCaseExp() As String
Private mCaseClean() As String

Public Sub TafqitSelfTest()
    Dim failed As Long, msg As String
    failed = TafqitGoldenFailures()
    msg = "InstaWords self-test: " & (mCount - failed) & " of " & mCount & " golden cases passed."
    Debug.Print msg
    If failed = 0 Then
        MsgBox msg, vbInformation, "InstaWords"
    Else
        MsgBox msg & vbCrLf & failed & " failed: see the Immediate window (Ctrl+G).", vbExclamation, "InstaWords"
    End If
End Sub

' Runs every golden case; returns the number of failures.
Public Function TafqitGoldenFailures() As Long
    Dim i As Long, failed As Long, got As Variant, cleaned As Variant
    LoadCases
    failed = 0
    For i = 1 To mCount
        got = TAFQIT(mCaseIn(i), mCaseUnit(i), mCaseHf(i), mCasePre(i), mCaseSuf(i), mCaseDec(i), mCaseLang(i))
        cleaned = TAFQIT_CLEAN(mCaseIn(i))
        If Not SameText(got, mCaseExp(i)) Then
            failed = failed + 1
            Debug.Print "FAIL " & mCaseId(i) & " TAFQIT"
            Debug.Print "  expected: " & CodePoints(mCaseExp(i))
            Debug.Print "  got:      " & CodePoints(got)
        ElseIf Not SameText(cleaned, mCaseClean(i)) Then
            failed = failed + 1
            Debug.Print "FAIL " & mCaseId(i) & " TAFQIT_CLEAN"
            Debug.Print "  expected: " & CodePoints(mCaseClean(i))
            Debug.Print "  got:      " & CodePoints(cleaned)
        End If
    Next i
    TafqitGoldenFailures = failed
End Function

Private Function SameText(ByVal v As Variant, ByVal expected As String) As Boolean
    If VarType(v) = vbString Then
        SameText = (v = expected)
    Else
        SameText = False
    End If
End Function

' "U+0631 U+064A ..." so that Arabic is readable in the Immediate window.
Private Function CodePoints(ByVal v As Variant) As String
    Dim i As Long, c As Long, s As String, out As String
    If VarType(v) <> vbString Then
        CodePoints = "(not text)"
        Exit Function
    End If
    s = v
    out = ""
    For i = 1 To Len(s)
        c = AscW(Mid(s, i, 1))
        If c < 0 Then c = c + 65536
        out = out & " U+" & Right("000" & Hex(c), 4)
    Next i
    CodePoints = Mid(out, 2)
End Function

Private Function U(ByVal hexList As String) As String
    Dim i As Long, d As Long, v As Long, inToken As Boolean, out As String
    out = ""
    v = 0
    inToken = False
    For i = 1 To Len(hexList)
        d = InStr(1, "0123456789ABCDEF", UCase(Mid(hexList, i, 1)))
        If d > 0 Then
            v = v * 16 + (d - 1)
            inToken = True
        ElseIf inToken Then
            out = out & ChrW(v)
            v = 0
            inToken = False
        End If
    Next i
    If inToken Then out = out & ChrW(v)
    U = out
End Function

Private Sub LoadCases()
    mCount = 0
    ReDim mCaseId(1 To ${cases.length})
    ReDim mCaseIn(1 To ${cases.length})
    ReDim mCaseUnit(1 To ${cases.length})
    ReDim mCaseHf(1 To ${cases.length})
    ReDim mCasePre(1 To ${cases.length})
    ReDim mCaseSuf(1 To ${cases.length})
    ReDim mCaseDec(1 To ${cases.length})
    ReDim mCaseLang(1 To ${cases.length})
    ReDim mCaseExp(1 To ${cases.length})
    ReDim mCaseClean(1 To ${cases.length})
${procs.map((_, p) => `    LoadCases${p + 1}`).join("\n")}
End Sub

${procs.join("\n\n")}
`;
}

// ---------------------------------------------------------------- build

/** CRLF, and checks that the text is safe for the VBA editor. */
function toBas(text, name) {
  const crlf = text.replace(/\r\n/g, "\n").replace(/\n/g, "\r\n");
  const bad = crlf.match(/[^\x09\x0a\x0d\x20-\x7e]/);
  if (bad) throw new Error(`${name}: non-ASCII output U+${bad[0].charCodeAt(0).toString(16)}`);
  const long = crlf.split("\r\n").find((l) => l.length > 1000);
  if (long) throw new Error(`${name}: line too long for VBA: ${long.slice(0, 60)}...`);
  return crlf;
}

/** Builds both modules from a units catalog and the golden cases. Returns their text (CRLF). */
export function buildVba(unitsData, golden) {
  new Catalog(unitsData); // throws on an invalid catalog (same checks as the core)
  const units = unitsData.units;
  const keys = units.map((u) => unitKeysOf(u));

  // Characters whose toLowerCase() can produce text found in a catalog key.
  const keyChars = new Set(keys.flat().join(""));
  const lowerExtras = [];
  for (let c = 0x80; c <= 0xffff; c++) {
    if (c >= 0xd800 && c <= 0xdfff) continue;
    const ch = String.fromCharCode(c);
    const lo = ch.toLowerCase();
    if (lo !== ch && [...lo].every((x) => keyChars.has(x))) lowerExtras.push([c, lo]);
  }

  const sourceHash = createHash("sha256")
    .update(template.replace(/\r\n/g, "\n")).update(JSON.stringify(LX)).update(JSON.stringify(EN_WORDS)).update(JSON.stringify(FR_WORDS))
    .update(String(FR_WORDS.ELIDES)).update(JSON.stringify(ERROR_MESSAGES)).update(JSON.stringify(unitsData))
    .digest("hex").slice(0, 12);

  const catalog = catalogProcs(units, keys);
  const generated = [lexiconProc(), englishProc(), frenchProc(), frenchElisionProc(), ...charProcs(lowerExtras), ...catalog.lines]
    .map((p) => p.join("\n")).join("\n\n");
  const header = [
    `' GENERATED by scripts/build-vba.mjs from vba/Tafqit.template.bas, src/lexicon.ts,`,
    `' src/lang/en.ts, src/lang/fr.ts, src/errors.ts and data/units.json (${units.length} units).`,
    `' Do not edit: edit the template or the sources and run "npm run build:vba".`,
    `' Source hash: ${sourceHash}`,
  ].join("\n");
  const constants = [
    ...ERROR_CODES.map((code, i) => `Private Const E_${code} As Long = ${i + 1}`),
    `Private Const ERROR_COUNT As Long = ${ERROR_CODES.length}`,
    `Private Const MAX_INTEGER_DIGITS As Long = ${LX.MAX_INTEGER_DIGITS}`,
    `Private Const UNIT_COUNT As Long = ${units.length}`,
    `Private Const KEY_COUNT As Long = ${catalog.keyCount}`,
  ].join("\n");

  return {
    tafqit: toBas(fill(template, { HEADER: header, CONSTANTS: constants, GENERATED: generated }), "Tafqit.bas"),
    tests: toBas(testsModule(golden), "TafqitTests.bas"),
    units: units.length,
    keys: catalog.keyCount,
    cases: golden.cases.length,
  };
}

function main(argv) {
  const opt = (name, def) => {
    const i = argv.indexOf(name);
    return i === -1 ? def : resolve(argv[i + 1]);
  };
  const unitsPath = opt("--units", root + "data/units.json");
  const outDir = opt("--out", root + "vba");
  const out = buildVba(JSON.parse(readFileSync(unitsPath, "utf8")), JSON.parse(read("tests/golden/cases.json")));
  mkdirSync(outDir, { recursive: true });
  writeFileSync(outDir + "/Tafqit.bas", out.tafqit);
  writeFileSync(outDir + "/TafqitTests.bas", out.tests);
  const lines = (t) => t.split("\r\n").length;
  console.log(`${outDir}/Tafqit.bas: ${out.units} units, ${out.keys} lookup names, ${lines(out.tafqit)} lines`);
  console.log(`${outDir}/TafqitTests.bas: ${out.cases} golden cases, ${lines(out.tests)} lines`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) main(process.argv.slice(2));
