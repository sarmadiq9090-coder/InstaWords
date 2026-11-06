# InstaWords لـ Excel (VBA) · InstaWords for Excel (VBA)

<div dir="rtl">

دالة `TAFQIT` من InstaWords تكتب المبلغ بالحروف داخل Excel بالعربية أو الإنجليزية أو الفرنسية، **بلا إنترنت**، وتعطي النص نفسه حرفاً بحرف الذي تعطيه مكتبة InstaWords (TypeScript) والموقع. تعمل على Windows وMac.

## التثبيت: الإضافة InstaWords.xlam (تجريبية، موصى بها)

الإضافة ملف واحد تثبّته في Excel مرة واحدة، فتعمل الدالة في كل مصنف تفتحه دون استيراد أي شيء ودون حفظ المصنف بصيغة ‎.xlsm. نزّلها من [صفحة Excel في الموقع](https://fastkiwi.app/instawords/excel.html) أو من ملفات [آخر إصدار](https://github.com/sarmadiq9090-coder/InstaWords/releases/latest/download/InstaWords.xlam) (`InstaWords.xlam`).

1. ضع `InstaWords.xlam` في مجلد ثابت لن تحذفه (مثل `المستندات\InstaWords`)، فـ Excel يحمّلها من مكانها كل مرة.
2. **إلغاء الحظر أولاً:** انقر الملف بزر الفأرة الأيمن ← **خصائص** ← في أسفل تبويب «عام» فعّل **«إلغاء الحظر»** (Unblock) ← **موافق**. يضع Windows على كل ملف منزَّل من الإنترنت «علامة الويب» (Mark of the Web)، ويحظر Excel الماكرو في الملفات التي تحملها، فلا تعمل الدالة دون هذه الخطوة. إن لم يظهر الخيار فالملف غير محظور.
3. في Excel: **ملف ← خيارات ← الوظائف الإضافية** ← في «إدارة» اختر **وظائف Excel الإضافية** ← **انتقال…** ← **استعراض…** ← اختر `InstaWords.xlam` ← **موافق**، وتأكد أن «InstaWords» محدد ثم **موافق**.
4. اكتب في أي مصنف `=TAFQIT(A1, "SAR")`.

### إن ظهر تنبيه أمني: مركز التوثيق

إن أظهر Excel تنبيهاً أمنياً بشأن الإضافة عند تشغيله، أو أعادت الدالة `#NAME?`، فالطريقة الأكثر أماناً أن تجعل مجلد الإضافة **موقعاً موثوقاً**:

1. **ملف ← خيارات ← مركز التوثيق ← إعدادات مركز التوثيق…**
2. **المواقع الموثوقة ← إضافة موقع جديد…** ← اختر المجلد الذي فيه `InstaWords.xlam` ← **موافق**.
3. أغلق Excel وافتحه من جديد.

لا تختر «تمكين كل وحدات الماكرو» في **إعدادات الماكرو**، فهو يسمح لأي ملف بتشغيل برامج على جهازك. الإعداد الافتراضي («تعطيل كل وحدات الماكرو مع الإعلام») مع موقع موثوق يكفي.

### حالة الإضافة

`InstaWords.xlam` يولّده `scripts/build-xlam.mjs` من `vba/Tafqit.bas` نفسه، بلا أي اعتماديات وبنتيجة متطابقة في كل بناء: حزمة OOXML من نوع `application/vnd.ms-excel.addin.macroEnabled.main+xml` فيها `xl/vbaProject.bin` (ملف CFB حسب [MS-CFB]، ومشروع VBA حسب [MS-OVBA] يحوي المصدر فقط دون p-code، فيترجمه Excel من المصدر عند الفتح). **بُني وفق المواصفات وتحققنا منه بقراءته من جديد** (`tests/vba/xlam.test.mjs` يفكّ كل تيار ويقارن الوحدة بـ `Tafqit.bas` بايتاً ببايت، و`olevba` من oletools يتحقق منه مستقلاً في CI)، **لكنه لم يُفتح بعد في Excel حقيقي**: يحتاج تجربة واحدة (البند 13 في [`docs/TESTING-CHECKLIST.md`](../docs/TESTING-CHECKLIST.md)). إن لم يعمل لديك فاستخدم الطريقة التالية وأخبرنا.

## البديل: استيراد Tafqit.bas في مصنف

لـ Excel على Mac، أو إن أردت الدالة داخل مصنف بعينه تُرسله إلى غيرك:

1. افتح المصنف، ثم اضغط `Alt+F11` لفتح محرر VBA (على Mac: Tools ← Macro ← Visual Basic Editor).
2. من القائمة **File ← Import File…** اختر الملف `vba/Tafqit.bas`. (واختيارياً `vba/TafqitTests.bas` للاختبار الذاتي.)
3. احفظ المصنف بصيغة **‎.xlsm** (مصنف ممكّن للماكرو)، وفعّل الماكرو عند فتحه.

لا تنسخ النص من الملف وتلصقه في المحرر؛ استورد الملف نفسه.

## الاستخدام

```
=TAFQIT(A1)                                    → بدون وحدة
=TAFQIT(A1, "SAR")
=TAFQIT(A1, "ريال سعودي", "مائة", "فقط", "لا غير")
=TAFQIT(A1, "دينار كويتي", "مئة")
=TAFQIT(A1, "SAR", , , , "error")              → خطأ بدل التقريب
=TAFQIT(A1, "SAR", "", "", "Only", "", "en")   → One Thousand Two Hundred Fifty Saudi Riyals ...
=TAFQIT(A1, "MAD", , , , , "fr")               → mille deux cent cinquante dirhams marocains ...
=TAFQIT_CLEAN(A1)                              → الرقم بعد التنقية: "١٢٣٫٤٥" ← 123.45
```

| المعامل | الوصف |
|---|---|
| `Number` | خلية أو نص أو رقم. يقبل الأرقام الهندية والفارسية، و`,` و`٬` فاصلاً للآلاف، و`٫` فاصلة عشرية. |
| `Unit` | معرّف الوحدة أو رمزها أو اسمها أو أي اسم بديل من `data/units.json` (مثل `SAR` أو `ريال سعودي`). الافتراضي بدون وحدة. |
| `HundredsForm` | `"مائة"` (الافتراضي) أو `"مئة"`، ويقبل أيضاً `1` و`2`. |
| `Prefix` / `Suffix` | نص قبل التفقيط وبعده، مثل «فقط» و«لا غير». |
| `Decimals` | `"round"` (الافتراضي: تقريب نصفي للأعلى إلى دقة العملة) أو `"error"`. |
| `Language` | لغة الناتج: `"ar"` (الافتراضي) أو `"en"` أو `"fr"`، بأي حالة أحرف. القيمة الفارغة تعني العربية. `HundredsForm` لا أثر له في الإنجليزية والفرنسية. |

المعامل `Language` هو السابع والأخير، فالصيغ القديمة تعمل كما هي.

عند الخطأ تُعيد الدالة رسالة عربية في كل اللغات (مثل «الوحدة غير معروفة.»، أو «لا تتوفر صيغة لهذه الوحدة باللغة المختارة.» إذا خلا ملف الوحدات من صيغ الوحدة بتلك اللغة) هي رسائل `ERROR_MESSAGES` في `src/errors.ts` دون التفاصيل التي تضيفها المكتبة بين قوسين. وخلية فيها خطأ مثل `#N/A` تُعاد كما هي.

**دقة الخلايا الرقمية:** يخزّن Excel الأرقام بدقة 15 خانة معنوية، فالدالة تقرأ الخلية الرقمية بـ 15 خانة (كما يعرضها Excel) ثم تعالجها نصاً. للمبالغ الطويلة (أكثر من 15 خانة مع الكسور) اكتب الرقم **نصاً** (`'1234567890123.45`) لتحصل على دقة تامة.

## الاختبار الذاتي

في مصنف استوردت فيه `Tafqit.bas` و`TafqitTests.bas` شغّل الماكرو `TafqitSelfTest` (`Alt+F8`، أو `F5` في المحرر). يقارن الناتج بكل الحالات الذهبية ويعرض النتيجة في رسالة، ويكتب تفاصيل أي فشل في نافذة Immediate (`Ctrl+G`).

## بعد تعديل الوحدات

لا تعدّل `Tafqit.bas` يدوياً، فهو مولَّد. ألفاظ الأعداد تُؤخذ من `src/lexicon.ts` و`src/lang/en.ts` و`src/lang/fr.ts`، وصيغ الوحدات بكل اللغات من `data/units.json`. بعد تعديل أي منها (أو `vba/Tafqit.template.bas`) شغّل:

```
npm run build:vba      # يولّد vba/Tafqit.bas و vba/TafqitTests.bas
npm run test:vba       # يتحقق من التطابق مع المكتبة، ومن الإضافة
npm run build:xlam     # يولّد out/InstaWords.xlam
```

ثم احذف الوحدة القديمة من المصنف (File ← Remove) واستورد الجديدة، أو استبدل ملف الإضافة وأعد تشغيل Excel.

</div>

---

## English

InstaWords' `TAFQIT` spells an amount in words inside Excel, in Arabic, English or French, **fully offline**, and matches the TypeScript core and the website character for character. Works on Excel for Windows and Mac (plain arrays, no `Scripting.Dictionary`).

### Install the add-in (recommended)

`InstaWords.xlam` makes `TAFQIT` available in every workbook, with nothing to import and no .xlsm needed. Download it from the [site's Excel page](https://fastkiwi.app/instawords/excel.html) or the [latest release](https://github.com/sarmadiq9090-coder/InstaWords/releases/latest/download/InstaWords.xlam).

1. Keep the file in a folder you will not delete; Excel loads it from there.
2. **Unblock it first:** right-click → **Properties** → tick **Unblock** → OK. Windows marks downloaded files (Mark of the Web) and Excel blocks macros in them.
3. Excel → **File → Options → Add-ins** → Manage: **Excel Add-ins** → **Go…** → **Browse…** → pick `InstaWords.xlam` → OK, and make sure InstaWords is ticked.
4. If Excel shows a security notice or `#NAME?`: **Trust Center → Trust Center Settings… → Trusted Locations → Add new location…** and add the add-in's folder. That is the safe option; do not choose "Enable all macros" under Macro Settings.

**Status:** `scripts/build-xlam.mjs` builds the add-in from `vba/Tafqit.bas` with no dependencies and byte-for-byte reproducibly: an OOXML package (`application/vnd.ms-excel.addin.macroEnabled.main+xml`) whose `xl/vbaProject.bin` is an [MS-CFB] compound file holding a source-only [MS-OVBA] project (module streams with MS-OVBA-compressed source, `MODULEOFFSET` 0 and a 7-byte `_VBA_PROJECT` with version 0xFFFF, so Excel compiles from source). It is **built to spec and verified by round-trip parsing** (`tests/vba/xlam.test.mjs` decompresses every stream with independent readers and compares the module with `Tafqit.bas` byte for byte; CI also checks it with oletools' `olevba`), but it **still needs one real-Excel smoke test** (item 13 of [`docs/TESTING-CHECKLIST.md`](../docs/TESTING-CHECKLIST.md)). If it fails for you, use the .bas below and please report it.

### Or import the module into one workbook

1. Press `Alt+F11` to open the VBA editor (Mac: Tools → Macro → Visual Basic Editor).
2. **File → Import File…** and pick `vba/Tafqit.bas` (optionally also `vba/TafqitTests.bas`).
3. Save the workbook as **.xlsm** and enable macros.

Import the file; do not copy-paste its text. The module is called `TafqitLib` because Excel returns `#NAME?` when a worksheet function has the same name as its module.

### Usage

```
=TAFQIT(Number, [Unit], [HundredsForm], [Prefix], [Suffix], [Decimals], [Language])
=TAFQIT(A1, "ريال سعودي", "مائة", "فقط", "لا غير")
=TAFQIT(A1, "KWD")
=TAFQIT(A1, "SAR", "", "", "Only", "", "en")
=TAFQIT(A1, "EUR", , , , , "fr")
=TAFQIT_CLEAN(A1)            ' cleaned number, e.g. "١٢٣٫٤٥" -> 123.45
```

- `Unit`: id, code, label or alias from `data/units.json`; matching trims, collapses spaces and lower-cases Latin letters (same as `unitKey` in `src/catalog.ts`). Default: no unit.
- `HundredsForm`: `"مائة"` (default) or `"مئة"`; `1`/`2` and empty also work.
- `Decimals`: `"round"` (default, half-up to the subunit) or `"error"`.
- `Language`: `"ar"` (default), `"en"` or `"fr"`, any case; empty means Arabic. It is the last argument, so existing formulas keep working. `HundredsForm` has no effect outside Arabic.
- Errors return the Arabic message from `ERROR_MESSAGES` in `src/errors.ts` in every language (including `NO_LANGUAGE_FORMS` when the catalog has no forms for the unit in that language), without the core's `(detail)` suffix. Cell errors such as `#N/A` are passed through.

**Numeric cells:** Excel stores numbers as doubles (15 significant digits). A numeric cell is converted with `CDec` (15 significant digits, what Excel displays) and `Str` (locale-independent), then processed as text; no floating-point arithmetic is done on amounts. For amounts with more than 15 significant digits, enter the number as text.

### Self-test

Import `TafqitTests.bas` and run `TafqitSelfTest` (`Alt+F8`). It checks every golden case, shows a summary box and prints failures (as code points) to the Immediate window (`Ctrl+G`).

### Regenerate

`Tafqit.bas` is generated from `vba/Tafqit.template.bas`, the number words in `src/lexicon.ts`, `src/lang/en.ts` (`EN_WORDS`) and `src/lang/fr.ts` (`FR_WORDS`), `src/errors.ts` and `data/units.json` (Arabic forms plus `i18n.en`/`i18n.fr`). After editing any of them:

```
npm run build:vba   # writes vba/Tafqit.bas and vba/TafqitTests.bas (pure ASCII, CRLF)
npm run test:vba    # runs both through a VBA-subset interpreter and compares with the core; checks the add-in
npm run build:xlam  # writes out/InstaWords.xlam (also built into the site and attached to releases)
```

The parity tests (`tests/vba/`) execute the generated VBA in Node with `tests/vba/harness/vba.mjs`, a transpiler for the small VBA subset this module uses, and compare ~62,000 inputs in the three languages with `dist/index.js`. Keep template edits within that subset.
