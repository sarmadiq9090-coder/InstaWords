# Changelog · سجل التغييرات

Each version's section is copied into its GitHub release notes by the release workflow
(`scripts/changelog-section.mjs`). Earlier versions: [GitHub Releases](https://github.com/sarmadiq9090-coder/tafket/releases).

## 2.3.0

<div dir="rtl">

- **InstaWords في متجر Microsoft**: حزمة MSIX رسمية، تتحدث من المتجر نفسه (المحدّث الداخلي معطّل في نسخة المتجر ويقول «التحديثات تأتي من Microsoft Store»)، وتبدأ مع Windows عبر مهمة بدء التشغيل الخاصة بالحزمة.
- **«عدة مبالغ»** بدل «دفعة» اسماً لتبويب تحويل عمود كامل، و«ما نسخته (الحافظة)» بدل «الحافظة».
- **إصلاحات**: شعار السند لم يكن يُحمَّل أبداً؛ عمود فارغ في «خيارات الصياغة» حين تكون لغة التفقيط إنجليزية أو فرنسية؛ حقول نافذة الطباعة المقصوصة؛ معاينة الطباعة تعرض الصفحة كاملة.
- **الحد الأدنى**: Windows 10 الإصدار 2004 (‏10.0.19041) أو أحدث.
- **العلامات التجارية**: InstaWords™ وFastKiwi™ باسم FastKiwi (Sarmad)؛ الرخصة تبقى MIT (انظر `TRADEMARKS.md`).

</div>

- **InstaWords on the Microsoft Store**: an official MSIX package that updates through the Store (the in-app updater is off in the Store build and says "Updates come from Microsoft Store") and starts with Windows through the package's startup task.
- **«عدة مبالغ»** is the Arabic name of the bulk tab (was «دفعة»), and the clipboard is now «ما نسخته (الحافظة)».
- **Fixes**: the voucher logo never loaded; an empty column in "Formatting options" for English/French output; clipped print-window fields; the print preview now shows the whole page.
- **Minimum**: Windows 10 version 2004 (10.0.19041) or later.
- **Trademarks**: InstaWords™ and FastKiwi™ of FastKiwi (Sarmad); the licence stays MIT (see `TRADEMARKS.md`).

## 2.2.0

<div dir="rtl">

- **إضافة Excel ‏`InstaWords.xlam`**: ثبّتها مرة واحدة فتصبح الدالة `=TAFQIT` متاحة في كل مصنف دون إنترنت، بلا استيراد وحدة VBA في كل ملف. صفحة Excel في الموقع تشرح التثبيت و«إلغاء الحظر» و«المواقع الموثوقة». (مبنية وفق المواصفات ومختبرة آلياً، وتحتاج تجربة في Excel حقيقي.)
- **سجل الأعطال**: أي خطأ غير متوقع، في الواجهة أو في الخلفية، يُكتب في `crash.log` داخل `%APPDATA%\FastKiwi\InstaWords` (الوقت والإصدار ونص الخطأ فقط، بلا مبالغ)، ولا يُرسل إلى أي مكان. أرفقه بنفسك عند الإبلاغ عن مشكلة.
- **أمان البناء**: رمز التوقيع الرقمي لا يصل إلا إلى خطوتي التوقيع في تشغيل الإصدار، داخل بيئة GitHub محمية.

</div>

- **Excel add-in `InstaWords.xlam`**: install once and `=TAFQIT` works in every workbook, offline, without importing the VBA module into each file. The site's Excel page covers installing, Unblock and Trusted Locations. (Built to spec and verified by parsing; needs a real-Excel smoke test.)
- **Crash log**: unexpected errors, on the UI thread or in the background, are written to `crash.log` in `%APPDATA%\FastKiwi\InstaWords` (time, version and the error only, no amounts). Nothing is sent; attach it to a problem report if you like.
- **Build security**: the code-signing token reaches only the two signing steps of a release run, inside a protected GitHub environment.
