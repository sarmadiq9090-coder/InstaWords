# تفقيط مفتوح · Tafket

[العربية](#العربية) · [English](#english)

---

<div dir="rtl">

## العربية

أداة **مجانية ومفتوحة المصدر (MIT)** لتحويل الأرقام إلى كلمات عربية (التفقيط) كما تُكتب في الشيكات والفواتير والمستندات المحاسبية. تعمل في المتصفح بلا خادم وبلا إنترنت، ومعها مكتبة TypeScript ووحدة Excel تعمل دون اتصال.

```
1250.75 ريال سعودي  ←  ألف ومائتان وخمسون ريالاً سعودياً وخمس وسبعون هللة
5.250 دينار كويتي   ←  خمسة دنانير كويتية ومائتان وخمسون فلساً
2,500 + فقط/لا غير  ←  فقط ألفان وخمسمائة ريال سعودي لا غير
```

### المزايا

- **تذكير وتأنيث صحيحان** للعدد والمعدود (ثلاثة ريالات، ثلاث ليرات، إحدى عشرة هللة).
- **صيغ مطابقة لعرف المالية والحسابات**: مائتان ريال، ألفان ريال، أحد عشر ألف ريال، مائتا ألف ريال.
- **أجزاء العملة** بأي نسبة من قوى العشرة: 100 هللة، و1000 فلس للدينار العراقي والكويتي والبحريني والأردني والريال العماني.
- **يقبل** الأرقام الهندية (١٢٣) والفارسية والغربية، وفواصل الآلاف (`,` `٬`) والفاصلة العشرية العربية (`٫`).
- **دقة كاملة**: الأرقام تُعالج نصوصاً ولا تُستعمل الأعداد العشرية (float)، حتى 999 تريليون.
- **صيغة المئات** حسب اختيارك: مائة أو مئة.
- **تفقيط متعدد بلا حد**: لصق آلاف الأرقام أو استيراد CSV، مع بادئة ولاحقة موحدتين، ونسخ النتائج، وتصديرها CSV.
- **يعمل دون اتصال** بعد الزيارة الأولى، بفضل Service Worker.
- **Excel دون إنترنت**: `=TAFQIT(A1; "ريال سعودي"; "مائة"; "فقط"; "لا غير")`.
- **الوحدات في ملف JSON**: العملات، والعملات الرقمية، ووحدات الوزن والحجم والمسافة والمساحة والزمن والعدّ والبيانات والسرعة. تضيف وحدة جديدة بلا أي تعديل في الكود.

### الاستخدام

**الموقع:** افتح `index.html` للتفقيط الفردي، و`bulk.html` للتفقيط المتعدد، و`excel.html` لشرح Excel.

**JavaScript / TypeScript:**

```ts
import { tafqit, tryTafqit, convert, createTafqit } from "tafket";

tafqit("1250.75", { unit: "SAR" });
// "ألف ومائتان وخمسون ريالاً سعودياً وخمس وسبعون هللة"

tafqit("٢٠٠", { unit: "ريال", hundredsForm: "مئة", prefix: "فقط", suffix: "لا غير" });
// "فقط مئتان ريال لا غير"

tryTafqit("abc");
// { ok: false, error: { code: "INVALID_FORMAT", message: "صيغة الرقم غير صحيحة." } }

tafqit("10.125", { unit: "SAR" });              // تقريب: «عشرة ريالات سعودية وثلاث عشرة هللة»
tafqit("10.125", { unit: "SAR", decimals: "error" }); // خطأ TOO_MANY_DECIMALS

convert({ the_number: "12", unit: "riyal", hundreds_form: "مائة" });
// { result: "اثنا عشر ريالاً" }

// فهرس وحدات مخصص
const t = createTafqit(myUnitsJson);
```

| الخيار | القيمة الافتراضية | الوصف |
|---|---|---|
| `unit` | `"none"` | معرّف الوحدة، أو رمزها، أو اسمها، أو أحد أسمائها البديلة |
| `hundredsForm` | `"مائة"` | `"مائة"` أو `"مئة"` |
| `prefix` / `suffix` | `""` | نص حر قبل النتيجة وبعدها |
| `decimals` | `"round"` | الخانات الزائدة عن دقة العملة: تقريب نصفي للأعلى، أو `"error"` |

**Excel:** استورد `vba/Tafqit.bas` (من Alt+F11 ثم File ثم Import File؛ اسم الوحدة داخله `TafqitLib`) واحفظ المصنف بصيغة `.xlsm`، ثم اكتب `=TAFQIT(A1; "ريال سعودي"; "مائة"; "فقط"; "لا غير")`. المعامل السادس الاختياري `"round"` أو `"error"`، و`=TAFQIT_CLEAN(A1)` يعيد الرقم بعد التنقية. مخرجات VBA مختبرة آلياً لتطابق النواة على قرابة 30 ألف حالة. التفاصيل في [`vba/README.md`](vba/README.md).

### إضافة وحدة

أضف كائناً إلى [`data/units.json`](data/units.json) يحمل الصيغ صريحة:

```json
{
  "id": "box", "category": "count", "label": "صندوق", "aliases": ["صندوق"],
  "forms": { "singular": "صندوق", "dual": "صندوقان", "plural": "صناديق", "accusative": "صندوقاً", "gender": "m" }
}
```

بعدها شغّل `npm test` و`npm run build:vba`. الموقع يقرأ `units.json` عند التشغيل، فيظهر التعديل فيه مباشرة.

### القواعد اللغوية

المواصفة الكاملة في [`docs/SPEC.md`](docs/SPEC.md): جداول الألفاظ، وقواعد التمييز، والإضافة، والكسور، والأخطاء. **قسم 16 فيها يجمع الحالات التي تحتاج مراجعة لغوية**، ونرحب بآراء المختصين.

### التطوير

```bash
npm install
npm test            # النواة + الحالات الذهبية
npm run test:vba    # تطابق مخرجات VBA مع النواة حرفاً بحرف
npm run test:site   # اختبار المتصفح (Playwright)
npm run build:site  # يبني الموقع في _site/
```

### النشر

النسخة الرسمية تُنشر على **https://fastkiwi.app/tafqit/** عبر Cloudflare Worker ([`wrangler.jsonc`](wrangler.jsonc)، [`cloudflare/worker.js`](cloudflare/worker.js)). يبني سير العمل [`.github/workflows/cloudflare.yml`](.github/workflows/cloudflare.yml) الموقع ويختبره وينشره عند كل دفع إلى الفرع الافتراضي، ويحتاج سرّين في المستودع: `CLOUDFLARE_API_TOKEN` و`CLOUDFLARE_ACCOUNT_ID`. وللنشر اليدوي: `npm run deploy:cf`.

والمجلد `_site/` (من `npm run build:site`) موقع ثابت كامل بمسارات نسبية، فيُرفع إلى أي استضافة ثابتة في الجذر أو في مسار فرعي.

### الترخيص

MIT. انظر [`LICENSE`](LICENSE). كُتب المشروع بأسلوب «الغرفة النظيفة»، من قواعد العربية وحدها، دون نسخ شيفرة أو نصوص من أي منتج آخر.

</div>

---

## English

**Free, open-source (MIT)** Arabic *tafqit*: converts numbers to Arabic words as written on cheques, invoices and accounting documents. Fully client-side and offline, with a zero-dependency TypeScript library and an offline Excel VBA module.

### Features

- Correct masculine/feminine agreement, dual and plural forms, following common finance/accounting usage (مائتان ريال، ألفان ريال، أحد عشر ألف ريال).
- Subunits with any power-of-ten ratio (100 halalas; 1000 fils for IQD/KWD/BHD/JOD, 1000 baisa for OMR).
- Accepts Arabic-Indic, Persian and Western digits, thousands separators and the Arabic decimal separator.
- Exact: numbers are processed as strings, never floats. Up to 999 trillion.
- `مائة` / `مئة` spelling option, prefix/suffix (e.g. «فقط … لا غير»).
- Unlimited bulk conversion, CSV import/export, copy, RTL, works offline (service worker).
- Excel: `=TAFQIT(A1, "ريال سعودي", "مائة", "فقط", "لا غير")` with no internet. Output is verified to match the core character for character.
- Units (≈40 currencies, crypto, weight, volume, length, area, time, count, data, speed) live in a JSON file you can extend without touching code.

### Usage

```ts
import { tafqit, tryTafqit, convert, createTafqit } from "tafket";

tafqit("1250.75", { unit: "SAR" });        // ألف ومائتان وخمسون ريالاً سعودياً وخمس وسبعون هللة
tafqit("11000", { unit: "riyal" });        // أحد عشر ألف ريال
tryTafqit("abc");                          // { ok: false, error: { code: "INVALID_FORMAT", … } }
convert({ the_number: "2", unit: "SAR" }); // { result: "ريالان سعوديان" }
```

Options: `unit` (id, code, label or alias; default `"none"`), `hundredsForm` (`"مائة"` | `"مئة"`), `prefix`, `suffix`, `decimals` (`"round"` half-up to the subunit — default — or `"error"`).
Error codes: `EMPTY`, `INVALID_FORMAT`, `NEGATIVE`, `TOO_LARGE`, `TOO_MANY_DECIMALS`, `FRACTION_NOT_SUPPORTED`, `UNKNOWN_UNIT`, `INVALID_OPTION`, `INVALID_CATALOG`.

The full linguistic specification is in [`docs/SPEC.md`](docs/SPEC.md). §16 lists the cases awaiting linguistic review. Excel instructions are in [`vba/README.md`](vba/README.md).

### Development

```bash
npm install
npm test           # core + 40 golden cases
npm run test:vba   # VBA ↔ core parity
npm run test:site  # browser smoke test (Playwright)
npm run build:site # deployable static site in _site/
```

Production: **https://fastkiwi.app/tafqit/**, served by a Cloudflare Worker (`wrangler.jsonc`) and deployed by `.github/workflows/cloudflare.yml` (needs `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID` secrets; manual: `npm run deploy:cf`). `_site/` also works on any static host, at the root or a sub-path.

Excel: import `vba/Tafqit.bas` (module `TafqitLib`), then `=TAFQIT(A1, "ريال سعودي", "مائة", "فقط", "لا غير", "round")`. VBA output is verified against the core on ~30,000 inputs via a VBA-subset harness.

See [`CONTRIBUTING.md`](CONTRIBUTING.md). License: MIT.
