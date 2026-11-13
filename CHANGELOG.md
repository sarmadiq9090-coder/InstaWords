# Changelog · سجل التغييرات

Each version's section is copied into its GitHub release notes by the release workflow
(`scripts/changelog-section.mjs`). Earlier versions: [GitHub Releases](https://github.com/sarmadiq9090-coder/InstaWords/releases).

## Unreleased

<div dir="rtl">

- **لا تخمين في الفواصل**: كانت الفاصلة تُعدّ فاصل آلاف دائماً، فتصير ‎`12,50`‎ يورو 1250، و‎`0,5`‎ دينار 5، و‎`1.250,75`‎ ريال 1.25 بصمت. الآن تُقبل الفاصلة كسراً في التفقيط بالفرنسية (‎`12,50`‎ = 12.50، و‎`1 250,75`‎ و‎`1.250,75`‎ = 1250.75)، وفي العربية والإنجليزية تبقى للآلاف بمجموعات من ثلاث خانات، وكل رقم يحتمل قراءتين يظهر له خطأ جديد يسأل «هل تقصد 12.50 أم 1250؟» بدل التخمين، في التطبيق والموقع وExcel.
- **تنبيه التقريب إلى صفر**: مبلغ أصغر من أصغر جزء في الوحدة (‎`0.004`‎ ريال) ما زال يُكتب «صفر ريال سعودي»، لكن مع تنبيه في الصفحة الواحدة والتفقيط المتعدد واختصار الحافظة والموقع.
- **‎`TAFQIT_CLEAN`‎ في Excel** تقبل معامل اللغة: ‎`=TAFQIT_CLEAN(A1, "fr")`‎.

</div>

- **No guessing on separators**: a comma was always a thousands separator, so `12,50` EUR silently became 1,250 euros, `0,5` TND 5 dinars and `1.250,75` SAR 1.25 riyals. French output now reads the comma as the decimal point (`12,50` = 12.50; `1 250,75` and `1.250,75` = 1250.75); Arabic and English keep it for thousands in groups of three, and any number that reads two ways gets the new error `AMBIGUOUS_SEPARATOR`, which asks “Did you mean 12.50 or 1250?” instead of guessing — in the app, the website and Excel.
- **Rounded-to-zero warning**: an amount below the unit's smallest part (`0.004` SAR) is still written “zero”, but now with a warning on the single page, the bulk tab, the clipboard shortcut and the website.
- **Excel `TAFQIT_CLEAN`** takes the output language: `=TAFQIT_CLEAN(A1, "fr")`.

## 2.2.0

<div dir="rtl">

- **إضافة Excel ‏`InstaWords.xlam`**: ثبّتها مرة واحدة فتصبح الدالة `=TAFQIT` متاحة في كل مصنف دون إنترنت، بلا استيراد وحدة VBA في كل ملف. صفحة Excel في الموقع تشرح التثبيت و«إلغاء الحظر» و«المواقع الموثوقة». (مبنية وفق المواصفات ومختبرة آلياً، وتحتاج تجربة في Excel حقيقي.)
- **سجل الأعطال**: أي خطأ غير متوقع، في الواجهة أو في الخلفية، يُكتب في `crash.log` داخل `%APPDATA%\FastKiwi\InstaWords` (الوقت والإصدار ونص الخطأ فقط، بلا مبالغ)، ولا يُرسل إلى أي مكان. أرفقه بنفسك عند الإبلاغ عن مشكلة.
- **أمان البناء**: رمز التوقيع الرقمي لا يصل إلا إلى خطوتي التوقيع في تشغيل الإصدار، داخل بيئة GitHub محمية.

</div>

- **Excel add-in `InstaWords.xlam`**: install once and `=TAFQIT` works in every workbook, offline, without importing the VBA module into each file. The site's Excel page covers installing, Unblock and Trusted Locations. (Built to spec and verified by parsing; needs a real-Excel smoke test.)
- **Crash log**: unexpected errors, on the UI thread or in the background, are written to `crash.log` in `%APPDATA%\FastKiwi\InstaWords` (time, version and the error only, no amounts). Nothing is sent; attach it to a problem report if you like.
- **Build security**: the code-signing token reaches only the two signing steps of a release run, inside a protected GitHub environment.
