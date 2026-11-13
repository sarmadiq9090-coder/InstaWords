# Changelog · سجل التغييرات

Each version's section is copied into its GitHub release notes by the release workflow
(`scripts/changelog-section.mjs`). Earlier versions: [GitHub Releases](https://github.com/sarmadiq9090-coder/InstaWords/releases).

## Unreleased

## 3.0.0

<div dir="rtl">

- **تغيير غير متوافق مع 2.x، لذلك الرقم 3.0.0**: بعض الأرقام المكتوبة نصاً كانت تُحوَّل وصارت تُرفض أو تُقرأ قراءة أخرى (انظر «لا تخمين في الفواصل» أدناه): ‎`12,50`‎ في العربية والإنجليزية يعطي الآن الخطأ الجديد `AMBIGUOUS_SEPARATOR` بدل 1250، و‎`1,250`‎ في الفرنسية صار 1.25 بدل 1250. يشمل ذلك مكتبة JavaScript والدالة `TAFQIT` في Excel لخلية فيها نص؛ الخلايا الرقمية في Excel لا تتأثر.
- **الرخصة**: InstaWords أصبح برنامجاً مملوكاً (مجانياً)؛ إشعارات الأطراف الثالثة دون تغيير.
- **متطلبات ويندوز**: المثبّت يرفض الآن إصدارات Windows 10 الأقدم من 1903، لأن ‎.NET Framework 4.8 غير مدمج فيها. والبرنامج لم يُجرَّب بعد على جهاز Windows حقيقي، وهذا مكتوب في README وملاحظات الإصدار.
- **حدود مكتوبة بوضوح** في README: صيغة الرقم (في العربية والإنجليزية الفاصلة للآلاف والنقطة للكسر، وفي الفرنسية آخر فاصل وحيد من نوعه هو الكسر، والرقم الملتبس يُرفض مع سؤال)، والإنجليزية على طريقة الشيكات الأمريكية (One Hundred One)، والخصوصية (آخر 100 مبلغ في `history.json` على جهازك فقط، ويُمسح بزر «مسح السجل»).
- **لا طباعة لكلمات مقصوصة**: إن لم يتّسع المبلغ كتابةً في أسطر الشيك أو شريط المبلغ حتى بأصغر خط مسموح، أو لم تتّسع نصوص السند في مساحته، يظهر تنبيه أحمر فوق المعاينة مع ما يمكن تغييره، ويتعطّل زر «طباعة» حتى يتّسع (كان آخر المبلغ يُقصّ على الورق دون تنبيه).
- **سطر ثالث للمبلغ كتابةً في الشيك** (اختياري، لكل ملف شيكات) للمبالغ الطويلة، ويزيد شريط المبلغ سطراً ثالثاً وحده عند الحاجة.
- **بطاقة «كيف أستخدمه؟»** (F1 أو زر ؟ أعلى النافذة): كتابة الأرقام وقاعدة الفاصلة، واختصار تحويل ما نسخته، و«عدة مبالغ»، ومعايرة طباعة الشيكات، وإضافة Excel. «حول» صار من زره فقط.
- إصلاح اتجاه «‎.NET» في تلميح نافذة الإبلاغ.
- **تحديث لا يترك البرنامج مغلقاً**: إذا لم يكتمل التحديث الصامت (ملف مستعمل، قرص ممتلئ، خطأ)، يُعيد المثبّت ملفات البرنامج كما كانت ويشغّل نسختك السابقة، ويخبرك البرنامج بأن التحديث لم يكتمل. وبعد تحديث ناجح يخبرك بالإصدار الجديد.
- **إزالة التثبيت لا تُغلق النسخة المحمولة**: لا يُغلَق إلا البرنامج الذي يعمل من مجلد التثبيت، وتبقى بياناتك في `%APPDATA%\FastKiwi\InstaWords`.
- **النسخة الأحدث تحلّ محلّ الأقدم**: إذا شغّلت إصداراً أحدث بينما يعمل إصدار أقدم في شريط المهام، تُغلَق القديمة (بعد حفظ إعداداتها) وتعمل الجديدة. الإصدار نفسه يُظهر النافذة المفتوحة كما كان.
- **نسخة المتجر**: «التحقق من وجود تحديث» يفتح صفحة InstaWords في Microsoft Store بدلاً من بند معطّل.
- **رسالة واضحة حين يكون GitHub مشغولاً**: «خدمة GitHub مشغولة الآن فتعذّر التحقق، حاول بعد ساعة» بدلاً من «لا يوجد اتصال».
- **حدود الاستيراد في «عدة مبالغ»**: ملف حتى 20 ميغابايت و100,000 رقم في المرة الواحدة، مع رسالة واضحة إن زاد (ولا يُحمَّل جزء منه بصمت)؛ وملف Excel ‏(.xlsx) أو ملف غير نصي يُرفض مع إرشاد إلى حفظه بصيغة CSV. ويُقرأ ملف CSV حتى وهو مفتوح في Excel.
- **لا تخمين في الفواصل**: كانت الفاصلة تُعدّ فاصل آلاف دائماً، فتصير ‎`12,50`‎ يورو 1250، و‎`0,5`‎ دينار 5، و‎`1.250,75`‎ ريال 1.25 بصمت. الآن تُقبل الفاصلة كسراً في التفقيط بالفرنسية (‎`12,50`‎ = 12.50، و‎`1 250,75`‎ و‎`1.250,75`‎ = 1250.75)، وفي العربية والإنجليزية تبقى للآلاف بمجموعات من ثلاث خانات، وكل رقم يحتمل قراءتين يظهر له خطأ جديد يسأل «هل تقصد 12.50 أم 1250؟» بدل التخمين، في التطبيق والموقع وExcel.
- **تنبيه التقريب إلى صفر**: مبلغ أصغر من أصغر جزء في الوحدة (‎`0.004`‎ ريال) ما زال يُكتب «صفر ريال سعودي»، لكن مع تنبيه في الصفحة الواحدة والتفقيط المتعدد واختصار الحافظة والموقع. ونافذة الطباعة لا تطبع «صفر» لمبلغ ليس صفراً: تنبيه أحمر فوق المعاينة (مع أرقام الصفحات في الطباعة المتعددة) وزر «طباعة» معطّل حتى يُصحَّح المبلغ.
- **‎`TAFQIT_CLEAN`‎ في Excel** تقبل معامل اللغة: ‎`=TAFQIT_CLEAN(A1, "fr")`‎.
- **أيقونة على سطح المكتب**: المثبّت يضع أيقونة InstaWords على سطح المكتب افتراضياً، ويمكنك إلغاء الخيار أثناء التثبيت.

</div>

- **Breaking change from 2.x, hence 3.0.0**: some numbers typed as text that used to convert are now refused or read differently (see "No guessing on separators" below): `12,50` with Arabic or English output now gives the new error `AMBIGUOUS_SEPARATOR` instead of 1,250, and `1,250` with French output now means 1.25, not 1250. This applies to the JavaScript library and to Excel's `TAFQIT` on a text cell; numeric Excel cells are unaffected. Code that handles every error code should add `AMBIGUOUS_SEPARATOR`; a successful `tryTafqit` result may now carry `warnings` (`ROUNDED_TO_ZERO`).
- **Licence**: InstaWords is now proprietary (free); third-party notices unchanged.
- **Windows requirement**: the installer now refuses Windows 10 builds older than 1903, which lack the built-in .NET Framework 4.8. The app is not yet tested on a real Windows device; README and the release notes now say so.
- **Limits stated plainly** in README: the number format (for Arabic and English, comma for thousands and dot for decimals; for French, a last separator that is the only one of its kind is the decimal point; ambiguous input is refused with a question), US cheque-style English (One Hundred One), and privacy (the last 100 amounts stay in `history.json` on your PC only; «Clear the history» removes them).
- **No more cut-off words on paper**: when the amount in words does not fit the cheque's or strip's lines even at the smallest allowed size, or a voucher's text does not fit its space, a red notice above the preview says what to change and Print stays disabled until it fits (the end of the amount used to be clipped on paper silently).
- **Optional third cheque line** for the amount in words (per cheque profile), and the words strip grows a third line by itself when needed.
- **"How to use" card** (F1 or the ? button in the header): typing numbers and the comma rule, the clipboard shortcut, Bulk, cheque calibration and the Excel add-in. About now opens from its button only.
- Fixed the direction of ".NET" in the Arabic report-window hint.
- **An update never leaves the app closed**: if a silent update does not finish (a file in use, a full disk, an error), the installer puts the program files back and starts your previous copy, which says the update did not go through. After a successful update the app names the new version.
- **Uninstall no longer closes portable copies**: only the copy running from the install folder is closed; your data in `%APPDATA%\FastKiwi\InstaWords` is kept.
- **A newer copy replaces an older one**: starting a newer version while an older one runs in the tray closes the old one (after it saves its settings) and runs the new one. The same version still just shows the open window.
- **Store copy**: "Check for updates" opens InstaWords' page in Microsoft Store instead of a disabled item.
- **A clear message when GitHub is busy**: "GitHub is busy, try again in an hour" instead of "offline".
- **Bulk import limits**: files up to 20 MB and 100,000 numbers at a time, with a clear message beyond that (nothing is silently cut); an Excel (.xlsx) or non-text file is refused with advice to save it as CSV. A CSV file can now be read while it is open in Excel.
- **No guessing on separators**: a comma was always a thousands separator, so `12,50` EUR silently became 1,250 euros, `0,5` TND 5 dinars and `1.250,75` SAR 1.25 riyals. French output now reads the comma as the decimal point (`12,50` = 12.50; `1 250,75` and `1.250,75` = 1250.75); Arabic and English keep it for thousands in groups of three, and any number that reads two ways gets the new error `AMBIGUOUS_SEPARATOR`, which asks “Did you mean 12.50 or 1250?” instead of guessing — in the app, the website and Excel.
- **Rounded-to-zero warning**: an amount below the unit's smallest part (`0.004` SAR) is still written “zero”, but now with a warning on the single page, the bulk tab, the clipboard shortcut and the website. The print dialog never prints “zero” for an amount that is not zero: a red notice above the preview (with the page numbers in a bulk job) and Print stays disabled until the amount is corrected.
- **Excel `TAFQIT_CLEAN`** takes the output language: `=TAFQIT_CLEAN(A1, "fr")`.
- **Desktop shortcut**: the installer now ticks "Create a desktop shortcut" by default; untick it during setup if you prefer.

## 2.2.0

<div dir="rtl">

- **إضافة Excel ‏`InstaWords.xlam`**: ثبّتها مرة واحدة فتصبح الدالة `=TAFQIT` متاحة في كل مصنف دون إنترنت، بلا استيراد وحدة VBA في كل ملف. صفحة Excel في الموقع تشرح التثبيت و«إلغاء الحظر» و«المواقع الموثوقة». (مبنية وفق المواصفات ومختبرة آلياً، وتحتاج تجربة في Excel حقيقي.)
- **سجل الأعطال**: أي خطأ غير متوقع، في الواجهة أو في الخلفية، يُكتب في `crash.log` داخل `%APPDATA%\FastKiwi\InstaWords` (الوقت والإصدار ونص الخطأ فقط، بلا مبالغ)، ولا يُرسل إلى أي مكان. أرفقه بنفسك عند الإبلاغ عن مشكلة.
- **أمان البناء**: رمز التوقيع الرقمي لا يصل إلا إلى خطوتي التوقيع في تشغيل الإصدار، داخل بيئة GitHub محمية.

</div>

- **Excel add-in `InstaWords.xlam`**: install once and `=TAFQIT` works in every workbook, offline, without importing the VBA module into each file. The site's Excel page covers installing, Unblock and Trusted Locations. (Built to spec and verified by parsing; needs a real-Excel smoke test.)
- **Crash log**: unexpected errors, on the UI thread or in the background, are written to `crash.log` in `%APPDATA%\FastKiwi\InstaWords` (time, version and the error only, no amounts). Nothing is sent; attach it to a problem report if you like.
- **Build security**: the code-signing token reaches only the two signing steps of a release run, inside a protected GitHub environment.
