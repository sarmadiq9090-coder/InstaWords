// Number words, docs/SPEC.md §2. Index = value.
import type { Gender, HundredsForm } from "./types.ts";

export const ZERO = "صفر";
export const WAW = " و";
export const DECIMAL_POINT_WORD = "فاصلة";

/** 1–10 (§2.1). */
export const ONES: Record<Gender, readonly string[]> = {
  m: ["", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة", "عشرة"],
  f: ["", "واحدة", "اثنتان", "ثلاث", "أربع", "خمس", "ست", "سبع", "ثماني", "تسع", "عشر"],
};

/** 11–19 (§2.2), index = value - 10. */
export const TEENS: Record<Gender, readonly string[]> = {
  m: ["", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر", "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر"],
  f: ["", "إحدى عشرة", "اثنتا عشرة", "ثلاث عشرة", "أربع عشرة", "خمس عشرة", "ست عشرة", "سبع عشرة", "ثماني عشرة", "تسع عشرة"],
};

/** Units digit inside 21–99 (§2.3). */
export const COMPOUND_ONES: Record<Gender, readonly string[]> = {
  m: ONES.m,
  f: ["", "إحدى", "اثنتان", "ثلاث", "أربع", "خمس", "ست", "سبع", "ثماني", "تسع"],
};

/** Tens 20–90 (§2.3), index = tens digit. */
export const TENS: readonly string[] = ["", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون"];

interface HundredsTable {
  /** index = hundreds digit (standalone forms). */
  words: readonly string[];
  /** 200 directly before a scale word (مائتا ألف). */
  dualConstruct: string;
}

/** Hundreds (§2.4). */
export const HUNDREDS: Record<HundredsForm, HundredsTable> = {
  "مائة": {
    words: ["", "مائة", "مائتان", "ثلاثمائة", "أربعمائة", "خمسمائة", "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة"],
    dualConstruct: "مائتا",
  },
  "مئة": {
    words: ["", "مئة", "مئتان", "ثلاثمئة", "أربعمئة", "خمسمئة", "ستمئة", "سبعمئة", "ثمانمئة", "تسعمئة"],
    dualConstruct: "مئتا",
  },
};

export interface ScaleWord {
  singular: string;
  dual: string;
  plural: string;
  accusative: string;
}

/** Scales (§2.5), index = k (1 = thousands … 4 = trillions). */
export const SCALES: readonly (ScaleWord | null)[] = [
  null,
  { singular: "ألف", dual: "ألفان", plural: "آلاف", accusative: "ألفاً" },
  { singular: "مليون", dual: "مليونان", plural: "ملايين", accusative: "مليوناً" },
  { singular: "مليار", dual: "ملياران", plural: "مليارات", accusative: "ملياراً" },
  { singular: "تريليون", dual: "تريليونان", plural: "تريليونات", accusative: "تريليوناً" },
];

export const MAX_INTEGER_DIGITS = 15;
