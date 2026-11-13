# Changelog · سجل التغييرات

Each version's section is copied into its GitHub release notes by the release workflow
(`scripts/changelog-section.mjs`). Earlier versions: [GitHub Releases](https://github.com/sarmadiq9090-coder/InstaWords/releases).

## Unreleased

<div dir="rtl">

- **لا طباعة لكلمات مقصوصة**: إن لم يتّسع المبلغ كتابةً في أسطر الشيك أو شريط المبلغ حتى بأصغر خط مسموح، أو لم تتّسع نصوص السند في مساحته، يظهر تنبيه أحمر فوق المعاينة مع ما يمكن تغييره، ويتعطّل زر «طباعة» حتى يتّسع (كان آخر المبلغ يُقصّ على الورق دون تنبيه).
- **سطر ثالث للمبلغ كتابةً في الشيك** (اختياري، لكل ملف شيكات) للمبالغ الطويلة، ويزيد شريط المبلغ سطراً ثالثاً وحده عند الحاجة.
- **بطاقة «كيف أستخدمه؟»** (F1 أو زر ؟ أعلى النافذة): كتابة الأرقام وقاعدة الفاصلة، واختصار تحويل ما نسخته، و«عدة مبالغ»، ومعايرة طباعة الشيكات، وإضافة Excel. «حول» صار من زره فقط.
- إصلاح اتجاه «‎.NET» في تلميح نافذة الإبلاغ.

</div>

- **No more cut-off words on paper**: when the amount in words does not fit the cheque's or strip's lines even at the smallest allowed size, or a voucher's text does not fit its space, a red notice above the preview says what to change and Print stays disabled until it fits (the end of the amount used to be clipped on paper silently).
- **Optional third cheque line** for the amount in words (per cheque profile), and the words strip grows a third line by itself when needed.
- **"How to use" card** (F1 or the ? button in the header): typing numbers and the comma rule, the clipboard shortcut, Bulk, cheque calibration and the Excel add-in. About now opens from its button only.
- Fixed the direction of ".NET" in the Arabic report-window hint.

## 2.2.0

<div dir="rtl">

- **إضافة Excel ‏`InstaWords.xlam`**: ثبّتها مرة واحدة فتصبح الدالة `=TAFQIT` متاحة في كل مصنف دون إنترنت، بلا استيراد وحدة VBA في كل ملف. صفحة Excel في الموقع تشرح التثبيت و«إلغاء الحظر» و«المواقع الموثوقة». (مبنية وفق المواصفات ومختبرة آلياً، وتحتاج تجربة في Excel حقيقي.)
- **سجل الأعطال**: أي خطأ غير متوقع، في الواجهة أو في الخلفية، يُكتب في `crash.log` داخل `%APPDATA%\FastKiwi\InstaWords` (الوقت والإصدار ونص الخطأ فقط، بلا مبالغ)، ولا يُرسل إلى أي مكان. أرفقه بنفسك عند الإبلاغ عن مشكلة.
- **أمان البناء**: رمز التوقيع الرقمي لا يصل إلا إلى خطوتي التوقيع في تشغيل الإصدار، داخل بيئة GitHub محمية.

</div>

- **Excel add-in `InstaWords.xlam`**: install once and `=TAFQIT` works in every workbook, offline, without importing the VBA module into each file. The site's Excel page covers installing, Unblock and Trusted Locations. (Built to spec and verified by parsing; needs a real-Excel smoke test.)
- **Crash log**: unexpected errors, on the UI thread or in the background, are written to `crash.log` in `%APPDATA%\FastKiwi\InstaWords` (time, version and the error only, no amounts). Nothing is sent; attach it to a problem report if you like.
- **Build security**: the code-signing token reaches only the two signing steps of a release run, inside a protected GitHub environment.
