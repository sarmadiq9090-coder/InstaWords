// French output, docs/SPEC.md §17.2. Traditional (pre-1990) orthography, lowercase.
import type { FrenchForms, Gender } from "../types.ts";
import { splitGroups, type LanguageModule } from "./types.ts";

const SMALL = [
  "zéro", "un", "deux", "trois", "quatre", "cinq", "six", "sept", "huit", "neuf", "dix",
  "onze", "douze", "treize", "quatorze", "quinze", "seize", "dix-sept", "dix-huit", "dix-neuf",
];
/** Tens 20–60; 70, 80 and 90 are built on soixante / quatre-vingt. */
const TENS = ["", "", "vingt", "trente", "quarante", "cinquante", "soixante"];
const FOUR_TWENTY = "quatre-vingt";
const CENT = "cent";
const MILLE = "mille";
/** Long scale: billion = 10^12. These are nouns: un million, deux millions. */
const SCALE_NOUNS = ["", "", "million", "milliard", "billion"];
const POINT = "virgule";
const SUBUNIT_JOINER = " et ";

const one = (g: Gender) => (g === "f" ? "une" : "un");

/**
 * 1..99. `final`: no other number word follows (only a noun, million/milliard/billion,
 * or nothing) — then 80 is written quatre-vingts.
 */
function below100(n: number, g: Gender, final: boolean): string {
  if (n === 1) return one(g);
  if (n < 20) return SMALL[n]!;
  const t = Math.floor(n / 10);
  const u = n % 10;
  if (t === 7 || t === 9) {
    const base = t === 7 ? TENS[6]! : FOUR_TWENTY;
    return t === 7 && u === 1 ? `${base} et ${SMALL[11]}` : `${base}-${SMALL[10 + u]}`; // soixante et onze, quatre-vingt-onze
  }
  if (t === 8) return u === 0 ? FOUR_TWENTY + (final ? "s" : "") : `${FOUR_TWENTY}-${u === 1 ? one(g) : SMALL[u]}`;
  if (u === 0) return TENS[t]!;
  return u === 1 ? `${TENS[t]} et ${one(g)}` : `${TENS[t]}-${SMALL[u]}`;
}

/** 1..999. cent takes s when multiplied and `final` with nothing after it in the group. */
function groupWords(g: number, gender: Gender, final: boolean): string {
  const h = Math.floor(g / 100);
  const r = g % 100;
  const parts: string[] = [];
  if (h === 1) parts.push(CENT);
  else if (h > 1) parts.push(`${SMALL[h]} ${CENT}${r === 0 && final ? "s" : ""}`);
  if (r > 0) parts.push(below100(r, gender, final));
  return parts.join(" ");
}

/** Integer words. `gender` applies to the last group only (un/une); higher groups are masculine. */
function integerWords(digits: string, gender: Gender): string {
  if (digits === "0") return SMALL[0]!;
  const groups = splitGroups(digits);
  const parts: string[] = [];
  for (let k = groups.length - 1; k >= 1; k--) {
    const g = groups[k]!;
    if (g === 0) continue;
    if (k === 1) parts.push(g === 1 ? MILLE : `${groupWords(g, "m", false)} ${MILLE}`); // mille: invariable, never "un mille"
    else parts.push(g === 1 ? `${one("m")} ${SCALE_NOUNS[k]}` : `${groupWords(g, "m", true)} ${SCALE_NOUNS[k]}s`);
  }
  if (groups[0]! > 0) parts.push(groupWords(groups[0]!, gender, true));
  return parts.join(" ");
}

/** "de " or "d'" before a noun that directly follows million/milliard/billion. h is treated as mute. */
function de(noun: string): string {
  return /^[aeiouhàâäéèêëîïôöûüœæ]/i.test(noun) ? "d'" : "de ";
}

export const french: LanguageModule<FrenchForms> = {
  info: { id: "fr", label: "Français", dir: "ltr", supportsHundredsForm: false },
  i18nForms: { gender: true },
  pickForms: (_ar, i18n) => i18n?.fr,
  plain(integer, fraction) {
    const intText = integerWords(integer, "m");
    if (!fraction) return intText;
    // Leading zeros are read as zéro, the rest as a whole number: douze virgule zéro cinq.
    const lead = fraction.match(/^0*/)![0].length;
    const rest = fraction.slice(lead);
    const words: string[] = new Array(lead).fill(SMALL[0]);
    if (rest) words.push(integerWords(rest, "m"));
    return `${intText} ${POINT} ${words.join(" ")}`;
  },
  count(digits, forms) {
    if (digits === "0") return `${SMALL[0]} ${forms.singular}`;
    if (digits === "1") return forms.one ?? `${one(forms.gender)} ${forms.singular}`;
    const words = integerWords(digits, forms.gender);
    // Exactly N million/milliard/billion: un million de dirhams, deux milliards d'euros.
    const link = /000000$/.test(digits) ? de(forms.plural) : "";
    return `${words} ${link}${forms.plural}`;
  },
  subunitJoiner: SUBUNIT_JOINER,
};
