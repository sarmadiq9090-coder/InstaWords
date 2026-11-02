import type { TafqitErrorCode } from "./types.ts";

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
};

export class TafqitError extends Error {
  readonly code: TafqitErrorCode;
  constructor(code: TafqitErrorCode, detail?: string) {
    super(detail ? `${ERROR_MESSAGES[code]} (${detail})` : ERROR_MESSAGES[code]);
    this.name = "TafqitError";
    this.code = code;
  }
}
