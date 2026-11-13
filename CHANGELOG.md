# Changelog · سجل التغييرات

Each version's section is copied into its GitHub release notes by the release workflow
(`scripts/changelog-section.mjs`). Earlier versions: [GitHub Releases](https://github.com/sarmadiq9090-coder/InstaWords/releases).

## Unreleased

<div dir="rtl">

- **تحديث لا يترك البرنامج مغلقاً**: إذا لم يكتمل التحديث الصامت (ملف مستعمل، قرص ممتلئ، خطأ)، يُعيد المثبّت ملفات البرنامج كما كانت ويشغّل نسختك السابقة، ويخبرك البرنامج بأن التحديث لم يكتمل. وبعد تحديث ناجح يخبرك بالإصدار الجديد.
- **إزالة التثبيت لا تُغلق النسخة المحمولة**: لا يُغلَق إلا البرنامج الذي يعمل من مجلد التثبيت، وتبقى بياناتك في `%APPDATA%\FastKiwi\InstaWords`.
- **النسخة الأحدث تحلّ محلّ الأقدم**: إذا شغّلت إصداراً أحدث بينما يعمل إصدار أقدم في شريط المهام، تُغلَق القديمة (بعد حفظ إعداداتها) وتعمل الجديدة. الإصدار نفسه يُظهر النافذة المفتوحة كما كان.
- **نسخة المتجر**: «التحقق من وجود تحديث» يفتح صفحة InstaWords في Microsoft Store بدلاً من بند معطّل.
- **رسالة واضحة حين يكون GitHub مشغولاً**: «خدمة GitHub مشغولة الآن فتعذّر التحقق، حاول بعد ساعة» بدلاً من «لا يوجد اتصال».
- **حدود الاستيراد في «عدة مبالغ»**: ملف حتى 20 ميغابايت و100,000 رقم في المرة الواحدة، مع رسالة واضحة إن زاد (ولا يُحمَّل جزء منه بصمت)؛ وملف Excel ‏(.xlsx) أو ملف غير نصي يُرفض مع إرشاد إلى حفظه بصيغة CSV. ويُقرأ ملف CSV حتى وهو مفتوح في Excel.

</div>

- **An update never leaves the app closed**: if a silent update does not finish (a file in use, a full disk, an error), the installer puts the program files back and starts your previous copy, which says the update did not go through. After a successful update the app names the new version.
- **Uninstall no longer closes portable copies**: only the copy running from the install folder is closed; your data in `%APPDATA%\FastKiwi\InstaWords` is kept.
- **A newer copy replaces an older one**: starting a newer version while an older one runs in the tray closes the old one (after it saves its settings) and runs the new one. The same version still just shows the open window.
- **Store copy**: "Check for updates" opens InstaWords' page in Microsoft Store instead of a disabled item.
- **A clear message when GitHub is busy**: "GitHub is busy, try again in an hour" instead of "offline".
- **Bulk import limits**: files up to 20 MB and 100,000 numbers at a time, with a clear message beyond that (nothing is silently cut); an Excel (.xlsx) or non-text file is refused with advice to save it as CSV. A CSV file can now be read while it is open in Excel.

## 2.2.0

<div dir="rtl">

- **إضافة Excel ‏`InstaWords.xlam`**: ثبّتها مرة واحدة فتصبح الدالة `=TAFQIT` متاحة في كل مصنف دون إنترنت، بلا استيراد وحدة VBA في كل ملف. صفحة Excel في الموقع تشرح التثبيت و«إلغاء الحظر» و«المواقع الموثوقة». (مبنية وفق المواصفات ومختبرة آلياً، وتحتاج تجربة في Excel حقيقي.)
- **سجل الأعطال**: أي خطأ غير متوقع، في الواجهة أو في الخلفية، يُكتب في `crash.log` داخل `%APPDATA%\FastKiwi\InstaWords` (الوقت والإصدار ونص الخطأ فقط، بلا مبالغ)، ولا يُرسل إلى أي مكان. أرفقه بنفسك عند الإبلاغ عن مشكلة.
- **أمان البناء**: رمز التوقيع الرقمي لا يصل إلا إلى خطوتي التوقيع في تشغيل الإصدار، داخل بيئة GitHub محمية.

</div>

- **Excel add-in `InstaWords.xlam`**: install once and `=TAFQIT` works in every workbook, offline, without importing the VBA module into each file. The site's Excel page covers installing, Unblock and Trusted Locations. (Built to spec and verified by parsing; needs a real-Excel smoke test.)
- **Crash log**: unexpected errors, on the UI thread or in the background, are written to `crash.log` in `%APPDATA%\FastKiwi\InstaWords` (time, version and the error only, no amounts). Nothing is sent; attach it to a problem report if you like.
- **Build security**: the code-signing token reaches only the two signing steps of a release run, inside a protected GitHub environment.
