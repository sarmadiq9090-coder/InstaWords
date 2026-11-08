# القاموس العربي الموحّد وقاعدة الأرقام · Unified Arabic number dictionary and the digit rule

<div dir="rtl">

## الخلاصة

- ملف البيانات: [`docs/arabic-numbers.json`](arabic-numbers.json)، ومخططه
  [`docs/arabic-numbers.schema.json`](arabic-numbers.schema.json).
- هو المصدر الواحد لألفاظ الأعداد العربية في كل تطبيقات FastKiwi: ينسخه كل تطبيق كما هو بدل أن يكتب
  الألفاظ بنفسه، فتبقى الكلمات واحدة في كل مكان.
- **بيانات فقط**: الألفاظ والأرقام والفواصل وقاعدة عرض الأرقام. قواعد التركيب (المثنى والجمع والتمييز
  والإضافة والعطف) في [`docs/SPEC.md`](SPEC.md)، ولا يحتاجها إلا تطبيق يكتب المبالغ بالحروف.
- **مولَّد لا مكتوب باليد**: يُنتَج من `src/lexicon.ts` و`src/normalize.ts` و`tests/golden/cases.json`
  بالسكربت `scripts/export-dictionary.mjs`. لا تعدّله يدوياً؛ الاختبار `tests/dictionary.test.ts` يفشل إن
  لم يطابق الملفُ ما يولّده السكربت، أو خالف المخطط.
- النسخة نفسها (بالبايت) تُنشر في الموقع على `https://fastkiwi.app/data/arabic-numbers.json` عند دمج طلبها هناك.

## قاعدة الأرقام (قرار المالك D-012)

| | القاعدة |
|---|---|
| العرض الافتراضي | الأرقام الغربية **123** |
| الخيار | الأرقام العربية الهندية **١٢٣**، يختارها المستخدم من الإعدادات ولا تُفرض عليه |
| «ذكّرني» | 123 فقط، بلا خيار |
| الإدخال | كل حقل يقبل الغربية `0–9` والعربية الهندية `٠–٩` والفارسية `۰–۹`، وفاصل الآلاف `,` أو `٬`، والفاصلة العشرية `.` أو `٫`، ويتجاهل المسافات (ومنها المسافة غير الفاصلة) |
| النص بالحروف | لا يتأثر بالخيار: «مائتان ريال» هي نفسها في الحالتين |

في الملف: `"digitDisplay": {"default": "latin", "options": ["latin", "arabic-indic"], "decision": "D-012"}`،
وجداول التحويل في `digits.toLatin` والفواصل في `digits.separators`.

## محتوى الملف

| المفتاح | المحتوى |
|---|---|
| `words.zero`، `words.decimalPoint` | صفر، فاصلة |
| `words.ones` | 1–10 مفردةً، لكل منها `m` (معدود مذكر) و`f` (معدود مؤنث) — SPEC §2.1 |
| `words.teens` | 11–19 بالرفع (اثنا/اثنتا عشر) — SPEC §2.2 |
| `words.compoundOnes` | الآحاد داخل 21–99 (واحد وعشرون، إحدى وعشرون) — SPEC §2.3 |
| `words.tens` | 20–90 |
| `words.hundreds` | الرسم الافتراضي (مائة) والرسمان (مائة، مئة): 100–900 و`dualConstruct` (مائتا قبل المرتبة) — SPEC §2.4 |
| `words.scales` | ألف، مليون، مليار، تريليون: المفرد والمثنى والجمع والمنصوب — SPEC §2.5 |
| `conjunction` | «و» وطريقة الوصل `" و"` (مسافة ثم و ملتصقة بالكلمة التالية) — SPEC §7 |
| `digits` | جداول التحويل والفواصل والمسافات المتجاهلة |
| `digitDisplay` | قاعدة الأرقام أعلاه |
| `examples` | الحالات الذهبية العربية في InstaWords (الرقم، الوحدة، الخيارات، النص المتوقع) لاختبار التطبيق الناسخ |
| `version`، `source.commit` | إصدار المحتوى (semver)، وآخر إيداع غيّر أحد ملفات المصدر |

## كيف ينسخه تطبيق آخر

1. انسخ `arabic-numbers.json` و`arabic-numbers.schema.json` كما هما إلى مستودع التطبيق (مثلاً
   `data/` أو `assets/`)، ولا تترجمهما ولا تعدّل الألفاظ.
2. اذكر في إيداع النسخ `version` و`source.commit` من الملف.
3. أضف اختباراً يقرأ الملف المنسوخ ويتحقق أن `schemaVersion` يساوي 1، وأن التطبيق يقرأ منه الألفاظ
   (لا من نسخة مكتوبة باليد). التطبيق الذي يكتب المبالغ بالحروف يمرّر `examples` كلها على محرّكه.
4. اعرض الأرقام حسب `digitDisplay`، واقبل في الإدخال كل ما في `digits`.
5. عند صدور إصدار جديد من القاموس: انسخه من جديد. رقم patch كلمة صُحّحت، minor حقل أُضيف، major حقل
   تغيّر أو حُذف (يحتاج مراجعة شيفرة القراءة).

## التحديث (في InstaWords)

بعد أي تغيير في الألفاظ، ارفع `DICTIONARY_VERSION` في `scripts/export-dictionary.mjs` ثم:

```sh
npm run export:dictionary -- --out docs/arabic-numbers.json    # يكتب الملف
npm run export:dictionary -- --check docs/arabic-numbers.json  # يفشل إن كان قديماً
npm test
```

ثم انسخ الملف نفسه إلى الموقع (`data/arabic-numbers.json`) في طلب دمج هناك.

</div>

## Summary

- Data file [`docs/arabic-numbers.json`](arabic-numbers.json), schema
  [`docs/arabic-numbers.schema.json`](arabic-numbers.schema.json).
- The single source of the Arabic number words for every FastKiwi app. Apps copy it unchanged instead of
  typing the words themselves.
- **Data only**: words, digits, separators and the digit-display rule. The grammar that combines the words
  is [`docs/SPEC.md`](SPEC.md); only an app that writes amounts in words needs it.
- **Generated**, from `src/lexicon.ts`, `src/normalize.ts` and `tests/golden/cases.json`, by
  `scripts/export-dictionary.mjs`. Don't edit it by hand: `tests/dictionary.test.ts` fails when the file is
  not the script's output or breaks the schema.
- The same bytes are published at `https://fastkiwi.app/data/arabic-numbers.json` once the site PR merges.

## The digit rule (owner decision D-012)

| | Rule |
|---|---|
| Default display | Western digits **123** |
| Option | Arabic-Indic digits **١٢٣**, a user setting, never forced |
| Remember Me | 123 only, no option |
| Input | every field accepts Western `0–9`, Arabic-Indic `٠–٩` and Persian `۰–۹`, `,` or `٬` for thousands, `.` or `٫` as the decimal point, and ignores whitespace (including no-break space) |
| Amount in words | unaffected by the option |

In the file: `digitDisplay` (`default: "latin"`, `options: ["latin", "arabic-indic"]`), with the maps in
`digits.toLatin` and the separators in `digits.separators`.

## Copying it into another app

1. Copy `arabic-numbers.json` and `arabic-numbers.schema.json` unchanged into the app's repo.
2. Name `version` and `source.commit` in the commit that copies them.
3. Add a test that reads the copied file, checks `schemaVersion` is 1 and that the app takes its words from
   it. An app that writes amounts in words runs all `examples` through its engine.
4. Display digits per `digitDisplay`; accept everything in `digits` on input.
5. On a new dictionary version, copy again: patch = a corrected word, minor = an added field, major = a
   changed or removed field (review the reading code).

## Updating (in InstaWords)

After a word changes, bump `DICTIONARY_VERSION` in `scripts/export-dictionary.mjs`, run the commands in the
Arabic section above, then copy the same file to the site's `data/arabic-numbers.json` in a PR there.
