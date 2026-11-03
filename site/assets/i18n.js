// InstaWords — interface strings shown by the page scripts, per interface language.
// The interface language is the page's own <html lang> (ar: /instawords/, en: /instawords/en/);
// it is separate from the output (tafqit) language picked in the converter.
// Static page text lives in the HTML of each language; only what the scripts write is here.
// English wording follows the Windows app (windows/InstaWords.App/Strings/Strings.en.xaml).

export const UI = document.documentElement.lang === "en" ? "en" : "ar";
export const UI_DIR = UI === "en" ? "ltr" : "rtl";

const plural = (n, one, many) => (n === 1 ? one : many);

const STRINGS = {
  ar: {
    // app.js
    "cat.none": "بدون وحدة",
    "cat.currency-arab": "عملات عربية",
    "cat.currency-world": "عملات عالمية",
    "cat.crypto": "عملات رقمية",
    "cat.generic-currency": "عملات بلا تحديد بلد",
    "cat.weight": "الأوزان",
    "cat.volume": "الأحجام والسعات",
    "cat.length": "الأطوال",
    "cat.area": "المساحات",
    "cat.time": "الزمن",
    "cat.count": "المعدودات",
    "cat.data": "سعة البيانات",
    "cat.speed": "السرعة",
    "cat.other": "وحدات أخرى",
    copied: "تم النسخ",
    copyFailed: "تعذّر النسخ",
    offlineReady: "جاهز للعمل دون اتصال",
    numberLocale: "ar-u-nu-latn",
    // single.js
    "single.empty": "اكتب مبلغاً في الحقل أعلاه، فيظهر هنا مكتوباً بالحروف.",
    "single.copied": "نُسخ النص إلى الحافظة.",
    "single.copyFailed": "تعذّر النسخ، حدّد النص وانسخه يدوياً.",
    // bulk.js
    "bulk.empty": "أدخل أرقاماً في المربع أعلاه، أو استورد ملف CSV، لتظهر النتائج هنا.",
    "bulk.rendering": (done, total) => `جارٍ عرض الصفوف… ${done} من ${total}`,
    "bulk.imported": (n, name) => `استُورد ${n} رقماً من الملف «${name}».`,
    "bulk.importFailed": "تعذّرت قراءة الملف. تأكد أنه ملف CSV أو نصي بترميز UTF-8.",
    "bulk.copied": (n, errs) => `نُسخ ${n} سطراً${errs ? `، وتُركت أسطر الأخطاء (${errs}) فارغة` : ""}.`,
    "bulk.copyFailed": "تعذّر النسخ.",
    "bulk.csvHeader": ["الرقم", "الرقم بعد التنقية", "التفقيط", "الخطأ"],
    "bulk.csvFile": "tafqit.csv",
    "bulk.exported": (n, file) => `صُدّر ${n} صفاً إلى ${file}.`,
    // release.js
    close: "إغلاق",
    kb: (n) => `${n} كيلوبايت`,
    mb: (n) => `${n} ميغابايت`,
    "dl.title": "InstaWords لويندوز: التنزيل وما الجديد",
    "dl.loading": "جارٍ التحقق من آخر إصدار…",
    "dl.offline1": "تعذّر الوصول إلى GitHub الآن (لا اتصال بالإنترنت أو الخدمة غير متاحة)، فلا يمكن عرض آخر إصدار وملاحظاته. يمكنك تنزيل الإصدار ",
    "dl.offline2": " مباشرة، وقد يكون هناك إصدار أحدث.",
    "dl.noSha": "غير متاح",
    "dl.version": "الإصدار",
    "dl.bundled": " (المضمّن في الموقع)",
    "dl.date": "تاريخ النشر",
    "dl.size": "حجم المثبّت",
    "dl.shaLabel": "بصمة المثبّت SHA-256",
    "dl.copySha": "نسخ البصمة",
    "dl.installer": "تنزيل المثبّت ",
    "dl.portable": "النسخة المحمولة (zip)",
    "dl.reqs": "المتطلبات",
    "dl.req1": "ويندوز 10 أو 11 (64 بت)",
    "dl.req2": "لا يحتاج صلاحيات المسؤول: يُثبَّت للمستخدم الحالي",
    "dl.req3": "يعمل دون إنترنت بعد التثبيت، والنسخة المحمولة لا تحتاج تثبيتاً",
    "dl.smart1": "البرنامج غير موقّع رقمياً بعد، فإن ظهر تنبيه SmartScreen فاختر «مزيد من المعلومات» ثم «تشغيل على أي حال». وللتحقق من الملف قارن بصمته بالأمر ",
    "dl.smart2": " في PowerShell.",
    "dl.notes": "ما الجديد في هذا الإصدار",
    "dl.releasesPage": "فتح صفحة الإصدارات على GitHub",
    "dl.allReleases": "كل الإصدارات على GitHub",
    "sup.title": "الإبلاغ عن مشكلة أو اقتراح",
    "sup.intro": "ملاحظتك تجعل InstaWords أدق. لا يُرسَل شيء تلقائياً من أي مكان: أنت من يراجع البلاغ ويرسله.",
    "sup.app": "برنامج ويندوز: ",
    "sup.appText": "اضغط زر «إبلاغ» أعلى نافذة البرنامج، أو اختر «الإبلاغ عن مشكلة أو اقتراح» من أيقونته بجانب الساعة. يجهّز البرنامج تقريراً فيه إصداره ونظامك دون أي مبلغ كتبته، فتنسخه أو تفتحه على GitHub.",
    "sup.excel1": "اذكر الصيغة كما كتبتها (مثل ",
    "sup.excelFormula": '=TAFQIT(A1, "SAR")',
    "sup.excel2": ") وقيمة الخلية والنص الذي ظهر والنص الذي توقعته، وإصدار Excel.",
    "sup.site": "الموقع: ",
    "sup.siteText": "اذكر الرقم والعملة والخيارات، والنص الذي ظهر والنص الصحيح في رأيك. ويمكنك إرفاق رابط مثل ?n=1250.75&unit=SAR يفتح المحوّل بالرقم والعملة نفسيهما.",
    "sup.fine": "تُستقبل البلاغات والاقتراحات على GitHub، وتحتاج حساباً مجانياً هناك.",
    "sup.open": "فتح بلاغ جديد على GitHub",
  },
  en: {
    "cat.none": "No unit",
    "cat.currency-arab": "Arab currencies",
    "cat.currency-world": "World currencies",
    "cat.crypto": "Cryptocurrencies",
    "cat.generic-currency": "Currencies without a country",
    "cat.weight": "Weight",
    "cat.volume": "Volume and capacity",
    "cat.length": "Length",
    "cat.area": "Area",
    "cat.time": "Time",
    "cat.count": "Counted items",
    "cat.data": "Data size",
    "cat.speed": "Speed",
    "cat.other": "Other units",
    "unit.none": "No unit",
    copied: "Copied",
    copyFailed: "Couldn't copy",
    offlineReady: "Ready to work offline",
    numberLocale: "en-US",
    "single.empty": "Type an amount in the field above to see it here in words.",
    "single.copied": "Copied to the clipboard.",
    "single.copyFailed": "Couldn't copy. Select the text and copy it by hand.",
    "bulk.empty": "Enter numbers in the box above, or import a CSV file, to see the results here.",
    "bulk.rendering": (done, total) => `Showing rows… ${done} of ${total}`,
    "bulk.imported": (n, name) => `Imported ${n} ${plural(n, "number", "numbers")} from “${name}”.`,
    "bulk.importFailed": "Couldn't read the file. Make sure it is a CSV or text file in UTF-8.",
    "bulk.copied": (n, errs) => `Copied ${n} ${plural(n, "line", "lines")}${errs ? `; the lines with errors (${errs}) were left empty` : ""}.`,
    "bulk.copyFailed": "Couldn't copy.",
    "bulk.csvHeader": ["Number", "Cleaned number", "Amount in words", "Error"],
    "bulk.csvFile": "amounts-in-words.csv",
    "bulk.exported": (n, file) => `Exported ${n} ${plural(n, "row", "rows")} to ${file}.`,
    close: "Close",
    kb: (n) => `${n} KB`,
    mb: (n) => `${n} MB`,
    "dl.title": "InstaWords for Windows: download and what's new",
    "dl.loading": "Checking for the latest version…",
    "dl.offline1": "GitHub can't be reached right now (no internet connection, or the service is unavailable), so the latest release and its notes can't be shown. You can download version ",
    "dl.offline2": " directly; a newer version may exist.",
    "dl.noSha": "not available",
    "dl.version": "Version",
    "dl.bundled": " (bundled with the site)",
    "dl.date": "Published",
    "dl.size": "Installer size",
    "dl.shaLabel": "Installer SHA-256",
    "dl.copySha": "Copy SHA-256",
    "dl.installer": "Download the installer ",
    "dl.portable": "Portable version (zip)",
    "dl.reqs": "Requirements",
    "dl.req1": "Windows 10 or 11 (64-bit)",
    "dl.req2": "No admin rights needed: installs for the current user",
    "dl.req3": "Works offline once installed; the portable version needs no installation",
    "dl.smart1": "The app is not code-signed yet, so if a SmartScreen warning appears, choose “More info”, then “Run anyway”. To verify the file, compare its SHA-256 with ",
    "dl.smart2": " in PowerShell.",
    "dl.notes": "What's new in this version",
    "dl.releasesPage": "Open the releases page on GitHub",
    "dl.allReleases": "All releases on GitHub",
    "sup.title": "Report a problem or suggestion",
    "sup.intro": "Your feedback makes InstaWords more accurate. Nothing is sent automatically from anywhere: you review the report and submit it yourself.",
    "sup.app": "Windows app: ",
    "sup.appText": "Click “Feedback” at the top of the app window, or choose “Report a problem or suggestion” from its icon next to the clock. The app prepares a report with its version and your system, without any amount you typed, for you to copy or open in GitHub.",
    "sup.excel1": "Give the formula as you typed it (such as ",
    "sup.excelFormula": '=TAFQIT(A1, "USD", , , , , "en")',
    "sup.excel2": "), the cell value, the text you got and the text you expected, and your Excel version.",
    "sup.site": "Website: ",
    "sup.siteText": "Give the number, the currency and the options, the text you got and the text you think is right. A link such as ?n=1250.75&unit=SAR&lang=en opens the converter with the same number, currency and language.",
    "sup.fine": "Reports and suggestions are received on GitHub, which needs a free account.",
    "sup.open": "Open a new issue on GitHub",
  },
};

/** Messages for the core's error codes (the core's own messages are Arabic). */
const ERRORS_EN = {
  EMPTY: "Please enter a number.",
  INVALID_FORMAT: "The number format is not valid.",
  NEGATIVE: "Negative numbers are not supported.",
  TOO_LARGE: "The number is larger than the limit (999 trillion).",
  TOO_MANY_DECIMALS: "There are more decimal places than this unit allows.",
  FRACTION_NOT_SUPPORTED: "This unit does not support fractions.",
  UNKNOWN_UNIT: "Unknown unit.",
  INVALID_OPTION: "Invalid option.",
  INVALID_CATALOG: "The units file is not valid.",
  NO_LANGUAGE_FORMS: "This unit has no wording in the chosen language.",
};

/** The interface string `key`; functions are called with `args`. Falls back to Arabic. */
export function t(key, ...args) {
  const v = STRINGS[UI][key] ?? STRINGS.ar[key];
  return typeof v === "function" ? v(...args) : v;
}

/**
 * The message for a core error in the interface language. Arabic pages show the core's own message
 * as is (identical to Excel and the Windows app in Arabic); English pages map the code, like the
 * Windows app's Loc.Error.
 */
export function errorMessage(error) {
  if (UI === "ar") return error.message;
  return ERRORS_EN[error?.code] ?? "Couldn't convert.";
}

/** Display name of a unit in the interface language (English: its English singular name). */
export function unitLabel(u) {
  if (UI === "ar") return u.label;
  if (u.id === "none") return t("unit.none");
  return u.i18n?.en?.singular ?? u.label;
}
