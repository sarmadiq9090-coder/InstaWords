import type { TafqitErrorCode, TafqitWarningCode } from "./types.ts";

export const ERROR_MESSAGES: Record<TafqitErrorCode, string> = {
  EMPTY: "الرجاء إدخال رقم.",
  INVALID_FORMAT: "صيغة الرقم غير صحيحة.",
  NEGATIVE: "الأعداد السالبة غير مدعومة.",
  TOO_LARGE: "الرقم أكبر من الحد المسموح (999 تريليون).",
  TOO_MANY_DECIMALS: "عدد الخانات العشرية أكبر مما تسمح به الوحدة.",
  FRACTION_NOT_SUPPORTED: "هذه الوحدة لا تدعم الكسور.",
  UNKNOWN_UNIT: "الوحدة غير معروفة.",
  INVALID_OPTION: "خيار غير صالح.",
  INVALID_CATALOG: "ملف الوحدات غير صالح.",
  NO_LANGUAGE_FORMS: "لا تتوفر صيغة لهذه الوحدة باللغة المختارة.",
  AMBIGUOUS_SEPARATOR: "الفواصل في الرقم تحتمل أكثر من قراءة. اكتبه بلا فواصل للآلاف، وضع قبل الكسر نقطة (أو فاصلة في الفرنسية)، مثل 1250.75.",
};

/** The detail of AMBIGUOUS_SEPARATOR: «هل تقصد 12.50 أم 1250؟» (start + readings joined by `or` + end). */
export const AMBIGUITY_QUESTION = Object.freeze({ start: "هل تقصد ", or: " أم ", end: "؟" });

/** Warnings: the words were written, but the user should look at them (docs/SPEC.md §13). */
export const WARNING_MESSAGES: Record<TafqitWarningCode, string> = {
  ROUNDED_TO_ZERO: "المبلغ أصغر من أصغر جزء في الوحدة، فقُرِّب إلى صفر.",
};

export class TafqitError extends Error {
  readonly code: TafqitErrorCode;
  /** AMBIGUOUS_SEPARATOR only: the possible readings of the input, e.g. ["12.50", "1250"]. */
  readonly readings?: readonly string[];
  constructor(code: TafqitErrorCode, detail?: string, readings?: readonly string[]) {
    super(detail ? `${ERROR_MESSAGES[code]} (${detail})` : ERROR_MESSAGES[code]);
    this.name = "TafqitError";
    this.code = code;
    if (readings !== undefined) this.readings = Object.freeze([...readings]);
  }
}
