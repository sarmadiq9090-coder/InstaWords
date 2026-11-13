# Changelog · سجل التغييرات

Each version's section is copied into its GitHub release notes by the release workflow
(`scripts/changelog-section.mjs`). Earlier versions: [GitHub Releases](https://github.com/sarmadiq9090-coder/InstaWords/releases).

## Unreleased

<!-- Merged on main since 2.2.0. In the release-day train PR, rename this heading to "## <version>" (the version in package.json); the release copies that section into its notes. -->

<div dir="rtl">

- **InstaWords في متجر Microsoft**: حزمة MSIX رسمية تتحدث من المتجر نفسه (المحدّث الداخلي معطّل في نسخة المتجر ويقول «التحديثات تأتي من Microsoft Store»)، وتبدأ مع Windows عبر مهمة بدء التشغيل الخاصة بالحزمة. تحتاج نسخة المتجر Windows 10 الإصدار 2004 (‏10.0.19041) أو أحدث.
- **المبالغ الصحيحة كما تُكتب في الواقع**: الدينار العراقي والليرة اللبنانية والسورية والريال اليمني تُعرض بلا كسور حين يكون المبلغ صحيحاً (1,750,000 دينار، لا 1,750,000.000)، وبقية العملات بمنازلها (45,750.00). المبلغ ذو الكسر يُعرض بكل منازله. العرض فقط؛ النص بالحروف والتقريب كما هما.
- **أيقونة جديدة**: ريشة قلم تخط سطراً، في البرنامج والموقع والمثبّت.
- **«الإبلاغ عن مشكلة»** يرسل التقرير بالبريد إلى support@fastkiwi.app مباشرة (بلا حاجة إلى حساب GitHub)، ويبقى النسخ وصفحة الدعم لمن لا بريد عنده.
- **«عدة مبالغ»** بدل «دفعة» اسماً لتبويب تحويل عمود كامل، و«ما نسخته (الحافظة)» بدل «الحافظة».
- **إصلاحات**: شعار السند لم يكن يُحمَّل أبداً؛ عمود فارغ في «خيارات الصياغة» حين تكون لغة التفقيط إنجليزية أو فرنسية؛ حقول نافذة الطباعة المقصوصة؛ معاينة الطباعة تعرض الصفحة كاملة.
- **إضافة Excel** موسومة «تجريبية» حتى تُجرَّب في Excel حقيقي.
- **الاسم والعلامات التجارية**: المستودع صار `sarmadiq9090-coder/InstaWords` (الروابط القديمة تُحوَّل تلقائياً، والتحديث يعمل كما هو)؛ InstaWords™ وFastKiwi™ باسم FastKiwi (Sarmad)، والرخصة تبقى MIT (انظر `TRADEMARKS.md`).

</div>

- **InstaWords on the Microsoft Store**: an official MSIX package that updates through the Store (the in-app updater is off in the Store build and says "Updates come from Microsoft Store") and starts with Windows through the package's startup task. The Store build needs Windows 10 version 2004 (10.0.19041) or later.
- **Whole amounts as written in practice**: IQD, LBP, SYP and YER show no decimals on a whole amount (1,750,000, not 1,750,000.000); other currencies keep their subunit digits (45,750.00). An amount with a fraction shows every digit. Display only: the words and the rounding are unchanged.
- **New icon**: a pen nib writing a stroke, in the app, the site and the installer.
- **Report a problem** sends the report by email to support@fastkiwi.app (no GitHub account needed); copy and the support page remain for machines without a mail app.
- **«عدة مبالغ»** is the Arabic name of the bulk tab (was «دفعة»), and the clipboard is now «ما نسخته (الحافظة)».
- **Fixes**: the voucher logo never loaded; an empty column in "Formatting options" for English/French output; clipped print-window fields; the print preview now shows the whole page.
- **Excel add-in** labelled experimental until it is tried in a real Excel.
- **Name and trademarks**: the repository is now `sarmadiq9090-coder/InstaWords` (old links redirect; updates keep working); InstaWords™ and FastKiwi™ of FastKiwi (Sarmad); the licence stays MIT (see `TRADEMARKS.md`).

## 2.2.0

<div dir="rtl">

- **إضافة Excel ‏`InstaWords.xlam`**: ثبّتها مرة واحدة فتصبح الدالة `=TAFQIT` متاحة في كل مصنف دون إنترنت، بلا استيراد وحدة VBA في كل ملف. صفحة Excel في الموقع تشرح التثبيت و«إلغاء الحظر» و«المواقع الموثوقة». (مبنية وفق المواصفات ومختبرة آلياً، وتحتاج تجربة في Excel حقيقي.)
- **سجل الأعطال**: أي خطأ غير متوقع، في الواجهة أو في الخلفية، يُكتب في `crash.log` داخل `%APPDATA%\FastKiwi\InstaWords` (الوقت والإصدار ونص الخطأ فقط، بلا مبالغ)، ولا يُرسل إلى أي مكان. أرفقه بنفسك عند الإبلاغ عن مشكلة.
- **أمان البناء**: رمز التوقيع الرقمي لا يصل إلا إلى خطوتي التوقيع في تشغيل الإصدار، داخل بيئة GitHub محمية.

</div>

- **Excel add-in `InstaWords.xlam`**: install once and `=TAFQIT` works in every workbook, offline, without importing the VBA module into each file. The site's Excel page covers installing, Unblock and Trusted Locations. (Built to spec and verified by parsing; needs a real-Excel smoke test.)
- **Crash log**: unexpected errors, on the UI thread or in the background, are written to `crash.log` in `%APPDATA%\FastKiwi\InstaWords` (time, version and the error only, no amounts). Nothing is sent; attach it to a problem report if you like.
- **Build security**: the code-signing token reaches only the two signing steps of a release run, inside a protected GitHub environment.
