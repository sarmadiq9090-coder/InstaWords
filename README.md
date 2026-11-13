# InstaWords — تفقيط

**🌐 https://fastkiwi.app/instawords/** · **🪟 [برنامج ويندوز / Windows app](https://github.com/sarmadiq9090-coder/InstaWords/releases/latest)** · من برامج [FastKiwi](https://fastkiwi.app/ar/)

[العربية](#العربية) · [English](#english)

---

<div dir="rtl">

## العربية

**InstaWords** أداة **مجانية** تكتب المبالغ بالحروف (التفقيط) **بالعربية والإنجليزية والفرنسية** كما تُكتب في الشيكات والفواتير والمستندات المحاسبية. تعمل في المتصفح بلا خادم وبلا إنترنت، ومعها **برنامج ويندوز** يعمل دون اتصال، ومكتبة TypeScript، ووحدة Excel.

```
1250.75 ريال سعودي  ←  ألف ومائتان وخمسون ريالاً سعودياً وخمس وسبعون هللة
5.250 دينار كويتي   ←  خمسة دنانير كويتية ومائتان وخمسون فلساً
2,500 + فقط/لا غير  ←  فقط ألفان وخمسمائة ريال سعودي لا غير
1250.75 SAR (en)    ←  One Thousand Two Hundred Fifty Saudi Riyals and Seventy-Five Halalas
1250.75 MAD (fr)    ←  mille deux cent cinquante dirhams marocains et soixante-quinze centimes
```

### المزايا

- **ثلاث لغات للتفقيط**: العربية والإنجليزية والفرنسية (وحدة مستقلة لكل لغة، وتُضاف لغات أخرى بسهولة).
- **برنامج ويندوز أصلي** (WPF، بلا متصفح) لـ Windows 10 الإصدار 1903 أو أحدث وWindows 11، يعمل دون إنترنت ولا يحتاج تثبيت أي متطلبات (‎.NET Framework 4.8 مدمج فيهما)، **ولم يُجرَّب بعد على جهاز Windows حقيقي**: تحويل فوري، وتبويب «عدة مبالغ» يُلصق فيه عمود من Excel، واختصار **Ctrl+Alt+T** لتحويل الرقم في الحافظة من أي برنامج، وتحديث تلقائي من GitHub، والإبلاغ عن مشكلة أو اقتراح ([التنزيل](https://github.com/sarmadiq9090-coder/InstaWords/releases/latest)، والتفاصيل في [`windows/README.md`](windows/README.md)).
- **تذكير وتأنيث صحيحان** للعدد والمعدود (ثلاثة ريالات، ثلاث ليرات، إحدى عشرة هللة).
- **صيغ مطابقة لعرف المالية والحسابات**: مائتان ريال، ألفان ريال، أحد عشر ألف ريال، مائتا ألف ريال.
- **أجزاء العملة** بأي نسبة من قوى العشرة: 100 هللة، و1000 فلس للدينار العراقي والكويتي والبحريني والأردني والريال العماني.
- **يقبل** الأرقام الهندية (١٢٣) والفارسية والغربية، وفواصل الآلاف (`,` `٬`) والفاصلة العشرية العربية (`٫`)، وفق قاعدة صيغة الرقم في «حدود يجب معرفتها» أدناه.
- **دقة كاملة**: الأرقام تُعالج نصوصاً ولا تُستعمل الأعداد العشرية (float)، حتى 999 تريليون.
- **صيغة المئات** حسب اختيارك: مائة أو مئة.
- **تفقيط متعدد بلا حد**: لصق آلاف الأرقام أو استيراد CSV، مع بادئة ولاحقة موحدتين، ونسخ النتائج، وتصديرها CSV.
- **يعمل دون اتصال** بعد الزيارة الأولى، بفضل Service Worker.
- **Excel دون إنترنت**: `=TAFQIT(A1; "ريال سعودي"; "مائة"; "فقط"; "لا غير")`، والمعامل السابع للغة: `"en"` أو `"fr"`.
- **الوحدات في ملف JSON**: العملات، والعملات الرقمية، ووحدات الوزن والحجم والمسافة والمساحة والزمن والعدّ والبيانات والسرعة. تضيف وحدة جديدة بلا أي تعديل في الكود.

### حدود يجب معرفتها

- **ويندوز:** يحتاج Windows 10 الإصدار 1903 أو أحدث، أو Windows 11 (‎.NET Framework 4.8 مدمج فيهما ابتداءً من 1903، والمثبّت يرفض الإصدارات الأقدم). **لم يُجرَّب بعد على جهاز Windows حقيقي**: يُختبر آلياً في GitHub Actions على Windows Server فقط، إلى أن يُجرَّب على Windows 10 و11 حقيقيين ([`docs/TESTING-CHECKLIST.md`](docs/TESTING-CHECKLIST.md)).
- **الإنجليزية:** على طريقة الشيكات الأمريكية، دون "and" داخل العدد: `101` ← `One Hundred One` (لا `One Hundred and One`). كلمة "and" تأتي فقط بين العملة وجزئها: `1250.75 SAR` ← `One Thousand Two Hundred Fifty Saudi Riyals and Seventy-Five Halalas`. كل كلمة تبدأ بحرف كبير (Title Case)، وأسماء الوحدات بالتهجئة البريطانية (Metre، Litre).
- **صيغة الرقم:** في العربية والإنجليزية الفاصلة `,` للآلاف فقط وفي مجموعات من 3 أرقام، والنقطة `.` للكسر العشري: `1,250.75`. الرقم الذي يحتمل أكثر من معنى (مثل `12,50` أو `1.250,75`) **يُرفض برسالة** توضح السبب، ولا يُخمَّن. وعند اختيار **الفرنسية** لغةً للنتيجة تكون الفاصلة `,` هي الفاصلة العشرية: `12,50` تعني 12.50.
- **الخصوصية:** يعمل دون إنترنت ولا يرسل أياً من بياناتك أو مبالغك. الاتصال الوحيد هو التحقق من التحديثات في نسخة المثبّت والنسخة المحمولة (طلب آخر إصدار من GitHub فقط، ويمكن إيقافه من الإعدادات)، ونسخة متجر Microsoft لا تتصل أبداً. يحفظ برنامج ويندوز آخر 100 مبلغ حوّلتها في `history.json` داخل `%APPDATA%\FastKiwi\InstaWords` على هذا الجهاز فقط، نصاً غير مشفّر؛ وتمسحها بزر «مسح السجل» في قائمة السجل أو في الإعدادات ← البيانات.

### الاستخدام

**الموقع:** افتح `index.html` للتفقيط الفردي، و`bulk.html` للتفقيط المتعدد، و`excel.html` لشرح Excel.

**JavaScript / TypeScript:**

```ts
import { tafqit, tryTafqit, convert, createTafqit, LANGUAGES } from "instawords";

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
| `language` | `"ar"` | `"ar"` أو `"en"` أو `"fr"` |
| `hundredsForm` | `"مائة"` | `"مائة"` أو `"مئة"` (للعربية فقط) |
| `prefix` / `suffix` | `""` | نص حر قبل النتيجة وبعدها |
| `decimals` | `"round"` | الخانات الزائدة عن دقة العملة: تقريب نصفي للأعلى، أو `"error"` |

**Excel:** ثبّت الإضافة `InstaWords.xlam` (من [صفحة Excel](https://fastkiwi.app/instawords/excel.html): خصائص الملف ← «إلغاء الحظر»، ثم ملف ← خيارات ← الوظائف الإضافية ← انتقال ← استعراض) فتعمل الدالة في كل مصنف؛ أو استورد `vba/Tafqit.bas` (من Alt+F11 ثم File ثم Import File؛ اسم الوحدة داخله `TafqitLib`) واحفظ المصنف بصيغة `.xlsm`، ثم اكتب `=TAFQIT(A1; "ريال سعودي"; "مائة"; "فقط"; "لا غير")`. المعامل السادس الاختياري `"round"` أو `"error"`، و`=TAFQIT_CLEAN(A1)` يعيد الرقم بعد التنقية. مخرجات VBA مختبرة آلياً لتطابق النواة على قرابة 30 ألف حالة. التفاصيل في [`vba/README.md`](vba/README.md).

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
npm test            # النواة + 107 حالات ذهبية
npm run test:vba    # تطابق مخرجات VBA مع النواة حرفاً بحرف
npm run test:site   # اختبار المتصفح (Playwright)
npm run test:dotnet # محرك C# لبرنامج ويندوز: مطابقة النواة حرفياً
npm run build:site  # يبني الموقع في _site/
```

### النشر

النسخة الرسمية منشورة على **https://fastkiwi.app/instawords/**: يُبنى الموقع بالأمر `npm run build:site`، ثم يُنسخ محتوى `_site/` إلى المجلد `instawords/` في مستودع [موقع FastKiwi](https://github.com/sarmadiq9090-coder/sarmadiq9090-coder.github.io). والمجلد `_site/` موقع ثابت كامل بمسارات نسبية، فيعمل على أي استضافة ثابتة في الجذر أو في مسار فرعي.

### الترخيص

برنامج مملوك مجاني للاستخدام على أجهزتك، انظر [`LICENSE`](LICENSE). الإصدارات حتى 2.2.0 (قبل 2026-11-09) تبقى متاحة برخصة MIT التي نُشرت بها. كُتب المشروع بأسلوب «الغرفة النظيفة»، من قواعد العربية وحدها، دون نسخ شيفرة أو نصوص من أي منتج آخر. والاسمان InstaWords™ وFastKiwi™ علامتان تجاريتان لا تشملهما الرخصة، انظر [`TRADEMARKS.md`](TRADEMARKS.md) ومصادر التنزيل الرسمية فيه.

</div>

---

## English

**InstaWords** is a **free** amount-in-words tool (*tafqit*) for cheques, invoices and accounting documents, in **Arabic, English and French**. Fully client-side and offline, with an **offline Windows app**, a zero-dependency TypeScript library and an Excel VBA module.

### Features

- Three output languages: Arabic, English (US cheque style with no "and" inside numbers, Title Case, British spelling for unit names) and French (traditional orthography), one module per language.
- Native Windows app (WPF, no browser engine) for Windows 10 version 1903 or newer and Windows 11, fully offline with nothing extra to install (.NET Framework 4.8 is built in), **not yet tested on a real Windows device**: instant conversion, bulk tab (paste a column from Excel), **Ctrl+Alt+T** converts the number on the clipboard from any program, auto-update from GitHub Releases, in-app problem/suggestion reports: [Releases](https://github.com/sarmadiq9090-coder/InstaWords/releases/latest), details in [`windows/README.md`](windows/README.md).
- Correct masculine/feminine agreement, dual and plural forms, following common finance/accounting usage (مائتان ريال، ألفان ريال، أحد عشر ألف ريال).
- Subunits with any power-of-ten ratio (100 halalas; 1000 fils for IQD/KWD/BHD/JOD, 1000 baisa for OMR).
- Accepts Arabic-Indic, Persian and Western digits, thousands separators and the Arabic decimal separator, following the number-format rule under "Limits" below.
- Exact: numbers are processed as strings, never floats. Up to 999 trillion.
- `مائة` / `مئة` spelling option, prefix/suffix (e.g. «فقط … لا غير»).
- Unlimited bulk conversion, CSV import/export, copy, RTL, works offline (service worker).
- Excel: `=TAFQIT(A1, "ريال سعودي", "مائة", "فقط", "لا غير")` with no internet. Output is verified to match the core character for character.
- Units (≈40 currencies, crypto, weight, volume, length, area, time, count, data, speed) live in a JSON file you can extend without touching code.

### Limits

- **Windows:** needs Windows 10 version 1903 or newer, or Windows 11 (.NET Framework 4.8 is built in from 1903; the installer refuses older versions). **Not yet tested on a real Windows device**: it is tested automatically on Windows Server in GitHub Actions only, until it has been tried on real Windows 10 and 11 PCs ([`docs/TESTING-CHECKLIST.md`](docs/TESTING-CHECKLIST.md)).
- **English output** is US cheque style, with no "and" inside numbers: `101` → `One Hundred One` (not `One Hundred and One`). "and" appears only between the currency and its subunit: `1250.75 SAR` → `One Thousand Two Hundred Fifty Saudi Riyals and Seventy-Five Halalas`. Every word is capitalised (Title Case); unit names use British spelling (Metre, Litre).
- **Number format:** for Arabic and English, `,` is a thousands separator only, in groups of 3 digits, and `.` is the decimal point: `1,250.75`. Input that could mean more than one amount (such as `12,50` or `1.250,75`) **is refused with a message** saying why; it is never guessed. With **French** output, `,` is the decimal separator: `12,50` means 12.50.
- **Privacy:** works offline and sends none of your data or amounts. The only connection is the update check in the installed and portable copies (it asks GitHub for the latest release only, and can be turned off in Settings); the Microsoft Store copy never connects. The Windows app keeps your last 100 amounts in `history.json` in `%APPDATA%\FastKiwi\InstaWords`, on this PC only, as plain text; clear them with «Clear the history» in the history list or in Settings → Data.

### Usage

```ts
import { tafqit, tryTafqit, convert, createTafqit, LANGUAGES } from "instawords";

tafqit("1250.75", { unit: "SAR" });        // ألف ومائتان وخمسون ريالاً سعودياً وخمس وسبعون هللة
tafqit("11000", { unit: "riyal" });        // أحد عشر ألف ريال
tryTafqit("abc");                          // { ok: false, error: { code: "INVALID_FORMAT", … } }
convert({ the_number: "2", unit: "SAR" }); // { result: "ريالان سعوديان" }
```

Options: `unit` (id, code, label or alias; default `"none"`), `language` (`"ar"` | `"en"` | `"fr"`), `hundredsForm` (Arabic only: `"مائة"` | `"مئة"`), `prefix`, `suffix`, `decimals` (`"round"` half-up to the subunit — default — or `"error"`).
Error codes: `EMPTY`, `INVALID_FORMAT`, `NEGATIVE`, `TOO_LARGE`, `TOO_MANY_DECIMALS`, `FRACTION_NOT_SUPPORTED`, `UNKNOWN_UNIT`, `INVALID_OPTION`, `INVALID_CATALOG`.

The full linguistic specification is in [`docs/SPEC.md`](docs/SPEC.md). §16 lists the cases awaiting linguistic review. Excel instructions are in [`vba/README.md`](vba/README.md).

### Development

```bash
npm install
npm test           # core + 107 golden cases
npm run test:vba   # VBA ↔ core parity
npm run test:site  # browser smoke test (Playwright)
npm run test:dotnet # C# engine (Windows app): exact parity with the core
npm run build:site # deployable static site in _site/
```

Production: **https://fastkiwi.app/instawords/**. Build with `npm run build:site` and copy `_site/` into the `instawords/` folder of the FastKiwi site repository. `_site/` uses relative paths, so it works on any static host, at the root or a sub-path.

Excel: install the `InstaWords.xlam` add-in (built by `scripts/build-xlam.mjs`; Unblock it, then File → Options → Add-ins → Go → Browse), or import `vba/Tafqit.bas` (module `TafqitLib`), then `=TAFQIT(A1, "ريال سعودي", "مائة", "فقط", "لا غير", "round")`. VBA output is verified against the core on ~30,000 inputs via a VBA-subset harness.

See [`CONTRIBUTING.md`](CONTRIBUTING.md). License: proprietary, free to use on your own devices, see [`LICENSE`](LICENSE); versions up to 2.2.0 (released before 2026-11-09) remain available under the MIT licence they were published with. "InstaWords" and "FastKiwi" are trademarks and not covered by the licence; see [`TRADEMARKS.md`](TRADEMARKS.md), which also lists the official download sources.
