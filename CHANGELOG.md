# Changelog · سجل التغييرات

Each version's section is copied into its GitHub release notes by the release workflow
(`scripts/changelog-section.mjs`). Earlier versions: [GitHub Releases](https://github.com/sarmadiq9090-coder/InstaWords/releases).

## Unreleased

<div dir="rtl">

- **الرخصة**: InstaWords أصبح برنامجاً مملوكاً (مجانياً)؛ إشعارات الأطراف الثالثة دون تغيير.
- **متطلبات ويندوز**: المثبّت يرفض الآن إصدارات Windows 10 الأقدم من 1903، لأن ‎.NET Framework 4.8 غير مدمج فيها. والبرنامج لم يُجرَّب بعد على جهاز Windows حقيقي، وهذا مكتوب في README وملاحظات الإصدار.
- **حدود مكتوبة بوضوح** في README: صيغة الرقم (الفاصلة للآلاف والنقطة للكسر، والرقم الملتبس يُرفض)، والإنجليزية على طريقة الشيكات الأمريكية (One Hundred One)، والخصوصية (آخر 100 مبلغ في `history.json` على جهازك فقط، ويُمسح بزر «مسح السجل»).

</div>

- **Licence**: InstaWords is now proprietary (free); third-party notices unchanged.
- **Windows requirement**: the installer now refuses Windows 10 builds older than 1903, which lack the built-in .NET Framework 4.8. The app is not yet tested on a real Windows device; README and the release notes now say so.
- **Limits stated plainly** in README: the number format (comma for thousands, dot for decimals, ambiguous input is refused), US cheque-style English (One Hundred One), and privacy (the last 100 amounts stay in `history.json` on your PC only; «Clear the history» removes them).

## 2.2.0

<div dir="rtl">

- **إضافة Excel ‏`InstaWords.xlam`**: ثبّتها مرة واحدة فتصبح الدالة `=TAFQIT` متاحة في كل مصنف دون إنترنت، بلا استيراد وحدة VBA في كل ملف. صفحة Excel في الموقع تشرح التثبيت و«إلغاء الحظر» و«المواقع الموثوقة». (مبنية وفق المواصفات ومختبرة آلياً، وتحتاج تجربة في Excel حقيقي.)
- **سجل الأعطال**: أي خطأ غير متوقع، في الواجهة أو في الخلفية، يُكتب في `crash.log` داخل `%APPDATA%\FastKiwi\InstaWords` (الوقت والإصدار ونص الخطأ فقط، بلا مبالغ)، ولا يُرسل إلى أي مكان. أرفقه بنفسك عند الإبلاغ عن مشكلة.
- **أمان البناء**: رمز التوقيع الرقمي لا يصل إلا إلى خطوتي التوقيع في تشغيل الإصدار، داخل بيئة GitHub محمية.

</div>

- **Excel add-in `InstaWords.xlam`**: install once and `=TAFQIT` works in every workbook, offline, without importing the VBA module into each file. The site's Excel page covers installing, Unblock and Trusted Locations. (Built to spec and verified by parsing; needs a real-Excel smoke test.)
- **Crash log**: unexpected errors, on the UI thread or in the background, are written to `crash.log` in `%APPDATA%\FastKiwi\InstaWords` (time, version and the error only, no amounts). Nothing is sent; attach it to a problem report if you like.
- **Build security**: the code-signing token reaches only the two signing steps of a release run, inside a protected GitHub environment.
