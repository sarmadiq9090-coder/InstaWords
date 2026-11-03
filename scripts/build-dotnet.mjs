// Generates windows/InstaWords.Core/Generated/*.g.cs, the data of the C# port.
//
//   windows/InstaWords.Core/*.cs  (hand-written logic, ports src/engine.ts, src/lang/*.ts …)
// + src/lexicon.ts                (Arabic number words)
// + src/lang/en.ts, src/lang/fr.ts (EN_WORDS, FR_WORDS)
// + src/lang/index.ts             (LANGUAGES)
// + src/errors.ts                 (error codes and messages)
// + src/normalize.ts              (character classes, probed through normalizeNumber)
// + data/units.json               (unit catalog, validated with src/catalog.ts)
//
// The generated files are committed. Run after changing any source above:
//
//   node scripts/build-dotnet.mjs           write the files
//   node scripts/build-dotnet.mjs --check   fail when a committed file is out of date
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { createHash } from "node:crypto";
import { resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { Catalog, unitKey } from "../src/catalog.ts";
import { ERROR_MESSAGES } from "../src/errors.ts";
import * as LX from "../src/lexicon.ts";
import { LANGUAGES, LANGUAGE_MODULES } from "../src/lang/index.ts";
import { EN_WORDS } from "../src/lang/en.ts";
import { FR_WORDS } from "../src/lang/fr.ts";
import { normalizeNumber } from "../src/normalize.ts";

const root = fileURLToPath(new URL("..", import.meta.url));
export const GENERATED_DIR = "windows/InstaWords.Core/Generated/";

// ---------------------------------------------------------------- checks on the sources

const fail = (msg) => { throw new Error(`build-dotnet: ${msg}`); };
const sameKeys = (name, obj, keys) => {
  const got = Object.keys(obj).join();
  if (got !== keys.join()) fail(`${name} keys changed (${got}); update scripts/build-dotnet.mjs and the C# port`);
};
const expectLen = (name, a, n) => { if (a.length !== n) fail(`${name} has ${a.length} entries, the C# port expects ${n}`); };

// The C# language code is written for these languages, in this order.
const LANGS = ["ar", "en", "fr"];
if (Object.keys(LANGUAGE_MODULES).join() !== LANGS.join()) fail(`languages changed: ${Object.keys(LANGUAGE_MODULES)}`);
if (LX.MAX_INTEGER_DIGITS !== 15) fail("MAX_INTEGER_DIGITS changed: check the scales of every language");
if (LX.SCALES.length !== 5 || LX.SCALES[0] !== null || LX.SCALES.slice(1).some((s) => !s)) fail("expected SCALES = [null, 4 scales]");
const HUNDREDS_NAMES = Object.keys(LX.HUNDREDS);
if (HUNDREDS_NAMES.join() !== ["مائة", "مئة"].join()) fail("expected HUNDREDS = {مائة, مئة} (the first is the default)");
for (const g of ["m", "f"]) {
  expectLen(`ONES.${g}`, LX.ONES[g], 11);
  expectLen(`TEENS.${g}`, LX.TEENS[g], 10);
  expectLen(`COMPOUND_ONES.${g}`, LX.COMPOUND_ONES[g], g === "m" ? 11 : 10);
}
expectLen("TENS", LX.TENS, 10);
sameKeys("EN_WORDS", EN_WORDS, ["SMALL", "TENS", "SCALES", "HUNDRED", "POINT", "SUBUNIT_JOINER", "HYPHEN"]);
sameKeys("FR_WORDS", FR_WORDS, ["SMALL", "TENS", "FOUR_TWENTY", "CENT", "MILLE", "SCALE_NOUNS", "POINT", "SUBUNIT_JOINER", "ET",
  "HYPHEN", "PLURAL_S", "ONE_F", "DE", "DE_ELIDED", "ELIDES"]);
expectLen("EN_WORDS.SMALL", EN_WORDS.SMALL, 20);
expectLen("EN_WORDS.TENS", EN_WORDS.TENS, 10);
expectLen("EN_WORDS.SCALES", EN_WORDS.SCALES, 5);
expectLen("FR_WORDS.SMALL", FR_WORDS.SMALL, 20);
expectLen("FR_WORDS.TENS", FR_WORDS.TENS, 7);
expectLen("FR_WORDS.SCALE_NOUNS", FR_WORDS.SCALE_NOUNS, 5);

const ERROR_CODES = Object.keys(ERROR_MESSAGES);

// ---------------------------------------------------------------- C# text

const LITERAL_OK = /[\p{L}\p{M}\p{N}\p{P}\p{S}]/u;
/** C# string literal. Printable text stays readable; anything invisible becomes \uXXXX. */
function cs(s) {
  if (s === null || s === undefined) return "null";
  let out = '"';
  for (let i = 0; i < s.length; i++) {
    const ch = s[i];
    const c = s.charCodeAt(i);
    if (ch === '"') out += '\\"';
    else if (ch === "\\") out += "\\\\";
    else if (c >= 0x20 && c < 0x7f) out += ch;
    else if (c >= 0xa0 && (c < 0xd800 || c > 0xdfff) && LITERAL_OK.test(ch) && c !== 0x2028 && c !== 0x2029) out += ch;
    else out += "\\u" + c.toString(16).toUpperCase().padStart(4, "0");
  }
  return out + '"';
}
const csArray = (a) => `new string[] { ${a.map(cs).join(", ")} }`;
const csBool = (b) => (b ? "true" : "false");
const pascal = (code) => code.toLowerCase().replace(/(^|_)([a-z])/g, (_, __, c) => c.toUpperCase());

/** Code-unit ranges [from, to] over the BMP where pred holds. */
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
const hex4 = (c) => "0x" + c.toString(16).toUpperCase().padStart(4, "0");
/** Body of a `static bool F(char c)` from code-unit ranges. */
function rangeTest(rs) {
  if (rs.length === 0) return "false";
  return rs.map(([a, b]) => (a === b ? `c == ${hex4(a)}` : `(c >= ${hex4(a)} && c <= ${hex4(b)})`)).join("\n            || ");
}

function file(name, sourceHash, sources, body) {
  return `// <auto-generated>
// GENERATED by scripts/build-dotnet.mjs from ${sources}.
// Do not edit: edit the sources and run "node scripts/build-dotnet.mjs".
// Source hash: ${sourceHash}
// </auto-generated>
#nullable enable

namespace InstaWords.Core.Internal
{
${body.replace(/\s+$/, "")}
}
`;
}

// ---------------------------------------------------------------- character classes

const CODE_UNITS = Array.from({ length: 0x10000 }, (_, c) => String.fromCharCode(c));
function probe(s) {
  try {
    return { cleaned: normalizeNumber(s).cleaned };
  } catch (e) {
    if (!e.code) throw e;
    return { code: e.code };
  }
}

function charClasses(keys) {
  const jsSpace = ranges((c) => /\s/.test(CODE_UNITS[c]));
  for (let c = 0; c <= 0xffff; c++) {
    if (/\s/.test(CODE_UNITS[c]) !== (CODE_UNITS[c].trim() === "")) fail(`trim and \\s disagree at U+${c.toString(16)}`);
  }

  // src/normalize.ts, probed: removed spaces, digits, separators and signs.
  const inputSpace = (c) => probe(CODE_UNITS[c]).code === "EMPTY";
  const digitOf = (c) => {
    const r = probe(CODE_UNITS[c]);
    return r.cleaned !== undefined && /^\d$/.test(r.cleaned) && !inputSpace(c) ? Number(r.cleaned) : -1;
  };
  const digits = [];
  for (let c = 0; c <= 0xffff; c++) if (digitOf(c) === 0) digits.push(c);
  for (const z of digits) for (let d = 0; d <= 9; d++) if (digitOf(z + d) !== d) fail(`digits starting at U+${z.toString(16)} are not 0..9`);
  const digitCount = ranges((c) => digitOf(c) >= 0).reduce((n, [a, b]) => n + b - a + 1, 0);
  if (digitCount !== digits.length * 10) fail("a digit is outside the 0..9 blocks");
  const isDigit = (c) => digits.some((z) => c >= z && c <= z + 9);
  const thousands = (c) => !inputSpace(c) && !isDigit(c) && probe("1" + CODE_UNITS[c] + "2").cleaned === "12";
  const decimal = (c) => probe("1" + CODE_UNITS[c] + "2").cleaned === "1.2";
  const minus = (c) => probe(CODE_UNITS[c] + "1").code === "NEGATIVE";
  const plus = (c) => !inputSpace(c) && !isDigit(c) && !thousands(c) && probe(CODE_UNITS[c] + "1").cleaned === "1"
    && probe(CODE_UNITS[c] + CODE_UNITS[c] + "1").code === "INVALID_FORMAT";
  if (!ranges(decimal).some(([a, b]) => a <= 0x2e && 0x2e <= b)) fail("'.' is not a decimal point any more");
  if (ranges(plus).flat().join() !== "43,43") fail("the plus sign set changed");

  // unitKey's toLowerCase(), restricted to the characters that can produce text found in a key.
  const keyChars = new Set(keys.join(""));
  for (const k of keyChars) {
    const c = k.charCodeAt(0);
    if (c >= 0xd800 && c <= 0xdfff) fail("a unit key contains a surrogate (astral character)");
    if (k.toLowerCase() !== k) fail(`unit key character U+${c.toString(16)} is not lower case`);
    if (/[Σςσ]/.test(k)) fail("a unit key contains a Greek sigma (context-dependent lower case)");
  }
  const lowerExtras = [];
  for (let c = 0x80; c <= 0xffff; c++) {
    if (c >= 0xd800 && c <= 0xdfff) continue;
    const lo = CODE_UNITS[c].toLowerCase();
    if (lo !== CODE_UNITS[c] && [...lo].every((x) => keyChars.has(x))) lowerExtras.push([c, lo]);
  }
  const elides = ranges((c) => FR_WORDS.ELIDES.test(CODE_UNITS[c]));

  const lowerCases = lowerExtras.map(([c, lo]) => `                case (char)${hex4(c)}: return ${cs(lo)};`).join("\n");
  return `    internal static class CharClasses
    {
        /// <summary>JavaScript /\\s/ (also what String.prototype.trim removes).</summary>
        internal static bool IsJsSpace(char c)
        {
            return ${rangeTest(jsSpace)};
        }

        /// <summary>Spaces removed from the input (src/normalize.ts SPACES).</summary>
        internal static bool IsInputSpace(char c)
        {
            return ${rangeTest(ranges(inputSpace))};
        }

        /// <summary>Thousands separators removed from the input (src/normalize.ts THOUSANDS).</summary>
        internal static bool IsThousandsSeparator(char c)
        {
            return ${rangeTest(ranges(thousands))};
        }

        /// <summary>Decimal points, read as '.' (src/normalize.ts DECIMAL and '.').</summary>
        internal static bool IsDecimalPoint(char c)
        {
            return ${rangeTest(ranges(decimal))};
        }

        /// <summary>A leading sign that means NEGATIVE.</summary>
        internal static bool IsMinusSign(char c)
        {
            return ${rangeTest(ranges(minus))};
        }

        /// <summary>Digit value 0..9 of an ASCII, Arabic-Indic or Persian digit, or -1.</summary>
        internal static int DigitValue(char c)
        {
${digits.map((z) => `            if (c >= ${hex4(z)} && c <= ${hex4(z + 9)}) return c - ${hex4(z)};`).join("\n")}
            return -1;
        }

        /// <summary>
        /// toLowerCase() of one non-ASCII character, or null when it is left as is. Only the
        /// characters whose lower case consists of characters found in a unit key are mapped:
        /// any other character cannot be part of a match either way.
        /// </summary>
        internal static string? LowerNonAscii(char c)
        {
            switch (c)
            {
${lowerCases}
                default: return null;
            }
        }

        /// <summary>FR_WORDS.ELIDES: a French noun starting with this code unit takes d'.</summary>
        internal static bool FrenchElides(char c)
        {
            return ${rangeTest(elides)};
        }
    }
`;
}

// ---------------------------------------------------------------- words

function arabicWords() {
  const hund = HUNDREDS_NAMES.map((n) => LX.HUNDREDS[n]);
  const scale = (k) => csArray(LX.SCALES.map((s) => (s ? s[k] : "")));
  return `    internal static class ArabicWords
    {
        internal const string Zero = ${cs(LX.ZERO)};
        internal const string Waw = ${cs(LX.WAW)};
        internal const string DecimalPoint = ${cs(LX.DECIMAL_POINT_WORD)};
        internal const int MaxIntegerDigits = ${LX.MAX_INTEGER_DIGITS};

        /// <summary>1–10, index = value.</summary>
        internal static readonly string[] OnesM = ${csArray(LX.ONES.m)};
        internal static readonly string[] OnesF = ${csArray(LX.ONES.f)};
        /// <summary>11–19, index = value - 10.</summary>
        internal static readonly string[] TeensM = ${csArray(LX.TEENS.m)};
        internal static readonly string[] TeensF = ${csArray(LX.TEENS.f)};
        /// <summary>Units digit inside 21–99.</summary>
        internal static readonly string[] CompoundOnesM = ${csArray(LX.COMPOUND_ONES.m)};
        internal static readonly string[] CompoundOnesF = ${csArray(LX.COMPOUND_ONES.f)};
        /// <summary>20–90, index = tens digit.</summary>
        internal static readonly string[] Tens = ${csArray(LX.TENS)};

        /// <summary>Accepted hundredsForm values; index 0 is the default.</summary>
        internal static readonly string[] HundredsForms = ${csArray(HUNDREDS_NAMES)};
        /// <summary>[hundreds form][hundreds digit].</summary>
        internal static readonly string[][] Hundreds =
        {
${hund.map((t) => `            ${csArray(t.words)},`).join("\n")}
        };
        /// <summary>200 directly before a scale word, per hundreds form.</summary>
        internal static readonly string[] HundredsDualConstruct = ${csArray(hund.map((t) => t.dualConstruct))};

        /// <summary>Scales, index = k (1 = thousands … 4 = trillions).</summary>
        internal static readonly string[] ScaleSingular = ${scale("singular")};
        internal static readonly string[] ScaleDual = ${scale("dual")};
        internal static readonly string[] ScalePlural = ${scale("plural")};
        internal static readonly string[] ScaleAccusative = ${scale("accusative")};
    }
`;
}

function englishWords() {
  const W = EN_WORDS;
  return `    internal static class EnglishWords
    {
        internal static readonly string[] Small = ${csArray(W.SMALL)};
        internal static readonly string[] Tens = ${csArray(W.TENS)};
        internal static readonly string[] Scales = ${csArray(W.SCALES)};
        internal const string Hundred = ${cs(W.HUNDRED)};
        internal const string Point = ${cs(W.POINT)};
        internal const string SubunitJoiner = ${cs(W.SUBUNIT_JOINER)};
        internal const string Hyphen = ${cs(W.HYPHEN)};
    }
`;
}

function frenchWords() {
  const W = FR_WORDS;
  const consts = [["FourTwenty", "FOUR_TWENTY"], ["Cent", "CENT"], ["Mille", "MILLE"], ["Point", "POINT"],
    ["SubunitJoiner", "SUBUNIT_JOINER"], ["Et", "ET"], ["Hyphen", "HYPHEN"], ["PluralS", "PLURAL_S"], ["OneF", "ONE_F"],
    ["De", "DE"], ["DeElided", "DE_ELIDED"]];
  for (const [, k] of consts) if (typeof W[k] !== "string") fail(`FR_WORDS.${k} is not a string`);
  return `    internal static class FrenchWords
    {
        internal static readonly string[] Small = ${csArray(W.SMALL)};
        /// <summary>20–60, index = tens digit.</summary>
        internal static readonly string[] Tens = ${csArray(W.TENS)};
        /// <summary>Long-scale nouns, index = k (2 = million …).</summary>
        internal static readonly string[] ScaleNouns = ${csArray(W.SCALE_NOUNS)};
${consts.map(([n, k]) => `        internal const string ${n} = ${cs(W[k])};`).join("\n")}
    }
`;
}

function languages() {
  return `    internal static class LanguageData
    {
        /// <summary>LANGUAGES of src/lang/index.ts, in picker order.</summary>
        internal static readonly LanguageInfo[] All =
        {
${LANGUAGES.map((l) => `            new LanguageInfo(${cs(l.id)}, ${cs(l.label)}, ${csBool(l.dir === "rtl")}, ${csBool(l.supportsHundredsForm)}),`).join("\n")}
        };
    }
`;
}

function errors() {
  // Public codes go in their own namespace-level class (see TafqitErrorCodes.g.cs).
  return `    internal static class ErrorMessages
    {
        /// <summary>Codes in src/errors.ts order.</summary>
        internal static readonly string[] Codes = ${csArray(ERROR_CODES)};

        /// <summary>ERROR_MESSAGES[code], without detail.</summary>
        internal static string Get(string code)
        {
            switch (code)
            {
${ERROR_CODES.map((c) => `                case ${cs(c)}: return ${cs(ERROR_MESSAGES[c])};`).join("\n")}
                default: throw new System.ArgumentOutOfRangeException(nameof(code), code, "unknown error code");
            }
        }
    }
`;
}

function errorCodes(sourceHash) {
  return `// <auto-generated>
// GENERATED by scripts/build-dotnet.mjs from src/errors.ts.
// Do not edit: edit the sources and run "node scripts/build-dotnet.mjs".
// Source hash: ${sourceHash}
// </auto-generated>
#nullable enable

namespace InstaWords.Core
{
    /// <summary>Values of <see cref="TafqitException.Code"/> and <see cref="TafqitResult.ErrorCode"/>.</summary>
    public static class TafqitErrorCodes
    {
${ERROR_CODES.map((c) => `        /// <summary>${ERROR_MESSAGES[c]}</summary>\n        public const string ${pascal(c)} = ${cs(c)};`).join("\n")}
    }
}
`;
}

// ---------------------------------------------------------------- units

/** Lookup names of a unit, as Catalog.add builds them (id first). */
export function unitKeysOf(u) {
  const idKey = unitKey(u.id);
  const names = new Set([u.label, u.code ?? "", ...(u.aliases ?? [])].map(unitKey).filter(Boolean));
  names.delete(idKey);
  return [idKey, ...names];
}

const nounForms = (f) => `new NounForms(${cs(f.singular)}, ${cs(f.dual)}, ${cs(f.plural)}, ${cs(f.accusative)}, ${csBool(f.gender === "f")}, ${cs(f.one)})`;
const foreignForms = (f) => (f ? `new ForeignForms(${cs(f.singular)}, ${cs(f.plural)}, ${cs(f.one)}, ${csBool(f.gender === "f")})` : "null");

function unitsFile(units) {
  const items = units.map((u) => {
    const L = [`                // ${u.id}`, "                new UnitData("];
    L.push(`                    id: ${cs(u.id)}, category: ${cs(u.category)}, label: ${cs(u.label)}, code: ${cs(u.code)},`);
    L.push(`                    aliases: ${csArray(u.aliases ?? [])},`);
    L.push(`                    forms: ${u.forms ? nounForms(u.forms) : "null"},`);
    L.push(`                    en: ${foreignForms(u.i18n?.en)},`);
    L.push(`                    fr: ${foreignForms(u.i18n?.fr)},`);
    if (u.subunit) {
      const s = u.subunit;
      L.push(`                    subunit: new SubunitData(${String(s.ratio).length - 1}, ${nounForms(s)},`);
      L.push(`                        ${foreignForms(s.i18n?.en)},`);
      L.push(`                        ${foreignForms(s.i18n?.fr)}),`);
    } else {
      L.push("                    subunit: null,");
    }
    L.push(`                    keys: ${csArray(unitKeysOf(u))}),`);
    return L.join("\n");
  });
  return `    internal static class BuiltInUnits
    {
        internal const int Count = ${units.length};

        /// <summary>A fresh copy of data/units.json (${units.length} units, in file order).</summary>
        internal static UnitData[] Create()
        {
            return new UnitData[]
            {
${items.join("\n")}
            };
        }
    }
`;
}

// ---------------------------------------------------------------- build

/** All generated files: { "Name.g.cs": text }. */
export function buildDotnet(unitsData) {
  new Catalog(unitsData); // throws on an invalid catalog (same checks as the core)
  const units = unitsData.units;
  const keys = units.flatMap((u) => unitKeysOf(u));

  const sourceHash = createHash("sha256")
    .update(readFileSync(fileURLToPath(import.meta.url), "utf8").replace(/\r\n/g, "\n"))
    .update(JSON.stringify(LX)).update(JSON.stringify(EN_WORDS)).update(JSON.stringify(FR_WORDS)).update(String(FR_WORDS.ELIDES))
    .update(JSON.stringify(LANGUAGES)).update(JSON.stringify(ERROR_MESSAGES)).update(readFileSync(root + "src/normalize.ts", "utf8").replace(/\r\n/g, "\n"))
    .update(JSON.stringify(unitsData))
    .digest("hex").slice(0, 16);

  const hashFile = `    internal static class GeneratedInfo
    {
        /// <summary>Hash of the sources the generated files were built from.</summary>
        internal const string SourceHash = ${cs(sourceHash)};
    }
`;
  return {
    sourceHash,
    files: {
      "ArabicWords.g.cs": file("ArabicWords", sourceHash, "src/lexicon.ts", arabicWords()),
      "EnglishWords.g.cs": file("EnglishWords", sourceHash, "src/lang/en.ts (EN_WORDS)", englishWords()),
      "FrenchWords.g.cs": file("FrenchWords", sourceHash, "src/lang/fr.ts (FR_WORDS)", frenchWords()),
      "CharClasses.g.cs": file("CharClasses", sourceHash,
        "src/normalize.ts, src/catalog.ts (unitKey), FR_WORDS.ELIDES and the unit keys of data/units.json", charClasses(keys)),
      "LanguageData.g.cs": file("LanguageData", sourceHash, "src/lang/index.ts (LANGUAGES)", languages()),
      "ErrorMessages.g.cs": file("ErrorMessages", sourceHash, "src/errors.ts", errors()),
      "TafqitErrorCodes.g.cs": errorCodes(sourceHash),
      "BuiltInUnits.g.cs": file("BuiltInUnits", sourceHash, "data/units.json", unitsFile(units)),
      "GeneratedInfo.g.cs": file("GeneratedInfo", sourceHash, "all of the above", hashFile),
    },
  };
}

function main(argv) {
  const check = argv.includes("--check");
  const out = buildDotnet(JSON.parse(readFileSync(root + "data/units.json", "utf8")));
  const dir = root + GENERATED_DIR;
  mkdirSync(dir, { recursive: true });
  const stale = [];
  for (const [name, text] of Object.entries(out.files)) {
    const path = dir + name;
    // Compare ignoring CRLF/LF so a Windows checkout with autocrlf does not count as stale.
    const current = existsSync(path) ? readFileSync(path, "utf8").replace(/\r\n/g, "\n") : null;
    if (current === text) continue;
    if (check) stale.push(name);
    else writeFileSync(path, text);
  }
  if (check && stale.length) {
    console.error(`${GENERATED_DIR}: out of date: ${stale.join(", ")}. Run "node scripts/build-dotnet.mjs".`);
    process.exit(1);
  }
  console.log(`${GENERATED_DIR}: ${Object.keys(out.files).length} files ${check ? "up to date" : "written"} (source hash ${out.sourceHash})`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) main(process.argv.slice(2));
