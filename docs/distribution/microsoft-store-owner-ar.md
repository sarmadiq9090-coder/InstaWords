# دليل المالك: إرسال InstaWords إلى Microsoft Store

<div dir="rtl">

هذا الدليل خطوة بخطوة لإرسال **InstaWords 2.2.1** إلى المتجر من Partner Center: من أين تنزّل الحزمة والصور، وماذا تختار في كل صفحة، وما النص الذي تلصقه في كل خانة. التفاصيل التقنية (كيف تُبنى الحزمة، وما يختلف في نسخة المتجر، ونتيجة اختبار WACK) في [`microsoft-store.md`](microsoft-store.md).

| القيمة في Partner Center | |
| --- | --- |
| اسم المنتج (محجوز) | InstaWords |
| Store ID | `9P62P4WQSQNK` |
| Package/Identity/Name | `FastKiwi.InstaWords` |
| Package/Identity/Publisher | `CN=8EFD6949-D088-481F-886D-82300679C5E5` |
| Package/Properties/PublisherDisplayName | `FastKiwi` |
| Package family name | `FastKiwi.InstaWords_sm1xjmj6v76yc` |
| إصدار هذا الإرسال | **2.2.1** (رقم الحزمة **2.2.1.0**) |
| ملف الحزمة | `InstaWords_2.2.1.0_x64.msix` |
| البريد للدعم | support@fastkiwi.app |

## 1. الهوية: `windows/packaging/msix/identity.json`

الملف فيه قيم Partner Center الحقيقية (الجدول أعلاه)، وقد تحقق منها اختبار التثبيت في GitHub Actions: ثُبّتت الحزمة باسم العائلة `FastKiwi.InstaWords_sm1xjmj6v76yc` نفسه. مصدر القيم: Partner Center ← Apps and games ← InstaWords ← Product management ← **Product identity**. لا تغيّرها إلا إذا تغيّرت هناك.

وفي الملف أيضاً الحقل **`storeVersion`** (الآن `"2.2.1"`): رقم إصدار نسخة المتجر حين يسبق رقم `package.json` (وهو 2.2.0 في هذا الفرع). منه يأخذ رقم الحزمة (2.2.1.0) ورقم الإصدار الذي تعرضه نافذة «حول» في نسخة المتجر. البناء يرفض أي `storeVersion` أصغر من رقم `package.json`.

## 2. تنزيل الحزمة والصور

من آخر تشغيل ناجح لفرع `claude/msstore-r2` (كل الاختبارات خضراء، ومنها اختبار التثبيت وWACK):

| الملف | الرابط المباشر |
| --- | --- |
| **الحزمة** `store-package` (فيها `InstaWords_2.2.1.0_x64.msix`) | https://github.com/sarmadiq9090-coder/InstaWords/actions/runs/42218450222/artifacts/13502541588 |
| **صور المتجر** (`InstaWords-2.2.0-screenshots`: الصور الثماني `store-*.png` مع صور الاختبارات الأخرى) | https://github.com/sarmadiq9090-coder/InstaWords/actions/runs/42218450222/artifacts/13501703662 |
| تقرير WACK وصور نسخة المتجر المثبّتة (`store-package-checks`، للاطلاع فقط) | https://github.com/sarmadiq9090-coder/InstaWords/actions/runs/42218450222/artifacts/13502496554 |

صفحة التشغيل نفسها: https://github.com/sarmadiq9090-coder/InstaWords/actions/runs/42218450222 (التشغيل رقم 42218450222، الإيداع `cf64c43`)

1. افتح الرابط وأنت مسجّل الدخول إلى GitHub، فيُنزَّل ملف zip.
2. فكّ ضغط `store-package.zip`: ستجد `InstaWords_2.2.1.0_x64.msix`. **لا توقّعه**: المتجر يوقّعه بنفسه.
3. فكّ ضغط ملف الصور، واستعمل منه الملفات الثمانية التي تبدأ بـ `store-` فقط (القسم 9).

(يحمل ملف الصور الرقم 2.2.0 في اسمه لأن اختبارات الواجهة تبني البرنامج من `package.json`؛ الصور لا يظهر فيها رقم إصدار. وتبقى الملفات في GitHub 90 يوماً بعد التشغيل.)

## 3. خطوات الإرسال في Partner Center

ادخل https://partner.microsoft.com/dashboard ← **Apps and games** ← **InstaWords** ← **Start your submission**. ثم املأ الصفحات بهذا الترتيب، واضغط **Save** في آخر كل صفحة:

1. **Packages** ← اسحب `InstaWords_2.2.1.0_x64.msix` إلى مربع الرفع. ينتظر Partner Center حتى يتحقق منها، ثم يعرض الهوية والإصدار 2.2.1.0 وx64 واللغتين. تحت **Device families** أبقِ **Windows 10/11 Desktop** وحده. (القسم 4)
2. **Pricing and availability** ← مجاني، كل الأسواق. (القسم 5)
3. **Properties** ← الفئة، والروابط، والبريد، والإقرارات. (القسم 6)
4. **Age ratings** ← الاستبيان. (القسم 7)
5. **Store listings** ← الإنجليزية (United States) والعربية فقط، مع النصوص والصور. (القسمان 8 و9)
6. **Supplemental info** ← **Additional Testing Information**: ملاحظات الاعتماد، ومبرّر runFullTrust. (القسم 10)
7. **Submission options** ← النشر تلقائياً بعد الاعتماد. (القسم 11)
8. **Submit to the Store**. يستغرق الاعتماد عادةً من يوم إلى ثلاثة أيام عمل، ويصلك بريد بالنتيجة.

## 4. Packages

- الحزمة واحدة: `InstaWords_2.2.1.0_x64.msix`. لا ترفع غيرها.
- رقم الحزمة 2.2.1.0. يجب أن يكون كل رفع لاحق أكبر منه (القسم 12).
- الحزمة تطلب **Windows 10 الإصدار 2004 (10.0.19041) أو أحدث**، وهو أول إصدار فيه ‎.NET Framework 4.8 مدمجاً، فلا يحتاج المستخدم تثبيت أي شيء آخر.
- الحزمة تعلن قدرة مقيّدة واحدة هي **runFullTrust** (برنامج سطح مكتب WPF). سيطلب Partner Center تبريرها (القسم 10).

## 5. Pricing and availability

| الخانة | الاختيار |
| --- | --- |
| Markets | **كل الأسواق** (All markets)، ومعها الأسواق المستقبلية إن عُرض الخيار |
| Visibility / Audience | **Public audience**، ظاهر وقابل للبحث في المتجر |
| Pricing | **Free** |
| Free trial | **No free trial** |
| Sale pricing / Add-ons | لا شيء |

## 6. Properties

| الخانة | القيمة |
| --- | --- |
| Category | **Business** ← Subcategory **Accounting & finance** (البرنامج يكتب المبالغ بالحروف للشيكات والفواتير والسندات ويطبعها: عمل محاسبي ومالي). إن طُلبت فئة ثانية: Productivity |
| Privacy policy URL | https://fastkiwi.app/privacy/ (الصفحة منشورة، وفيها قسم InstaWords يذكر أن نسخة المتجر لا تتصل بالإنترنت من تلقاء نفسها) |
| Website | https://fastkiwi.app/instawords/ |
| Support contact info | **support@fastkiwi.app** |
| System requirements | لا شيء خاص (اتركها فارغة، أو: Windows 10 2004 أو أحدث، x64) |
| Product declarations | ✔ «customers can install to alternate drives». واترك الباقي دون علامة: لا برامج تشغيل ولا خدمات NT، ولا نظام شراء خاص، ولا ادّعاء باختبار إمكانية الوصول (لم يُختبر بقارئ شاشة على جهاز حقيقي) |

## 7. Age ratings (استبيان IARC)

اختر الفئة **Productivity, Utility, Reference, Educational or other app** (ليس لعبة، وليس تطبيق تواصل)، ثم أجب:

| السؤال | الإجابة |
| --- | --- |
| العنف أو الدم أو الرعب | No |
| محتوى جنسي أو عُري | No |
| ألفاظ نابية أو فكاهة فظّة أو تمييز | No |
| المخدرات أو الكحول أو التبغ | No |
| القمار أو محاكاته أو الرهان بالمال | No |
| يتواصل المستخدمون أو يتبادلون محتوى (دردشة، مشاركة، محتوى ينشئه المستخدم ويراه غيره) | No |
| يشارك موقع المستخدم مع آخرين | No |
| شراء سلع رقمية داخل البرنامج | No |
| وصول غير مقيّد إلى الإنترنت (متصفح أو محرك بحث داخل البرنامج) | No (الروابط تُفتح في متصفح المستخدم فقط عند النقر) |
| يجمع معلومات شخصية أو يشاركها مع طرف ثالث | No (نسخة المتجر لا تتصل بالإنترنت، ولا شيء يُرسل) |
| يروّج لمنتجات مقيّدة بالعمر أو يبيعها | No |

النتيجة المتوقعة: أدنى تصنيف في كل مكان (IARC 3+، ESRB Everyone، PEGI 3، USK 0).

## 8. Store listings

**اللغات:** الحزمة تعلن لغتين فقط: **`en-US`** و**`ar`** (في `<Resources>` في `AppxManifest.xml`، وفي `Strings/en-US/resources.resw` و`Strings/ar/resources.resw`). لذلك يُفترض أن يعرض Partner Center صفحتين: **English (United States)** و**Arabic**. إن أضاف لغة أخرى استنتجها من الحزمة، فاحذفها من **Store listings ← Manage additional languages**، ولا تلصق فيها النص الإنجليزي: واجهة البرنامج مترجمة إلى العربية والإنجليزية فقط. (المبلغ بالحروف بالفرنسية ميزة في التحويل، لا لغة واجهة، فلا صفحة فرنسية.)

حدود Partner Center: الوصف حتى 10,000 حرف، والوصف المختصر حتى 1,000، وحتى 20 ميزة (200 حرف لكل منها)، وحتى 7 كلمات بحث (30 حرفاً لكل منها و21 كلمة إجمالاً)، و«ما الجديد» حتى 1,500. كل النصوص أدناه داخل هذه الحدود.

### 8.1 English (United States)

</div>

**Product name:** InstaWords

**Description**

> InstaWords writes amounts in words for cheques, invoices, receipts and payment vouchers, in Arabic, English and French, with more than 50 currencies and over 50 other units (weight, length, area, time, quantities…). Writing the amount in words, as cheques and vouchers require, is called tafqit (تفقيط) in Arabic.
>
> Type a number and the words appear as you type. Arabic grammar is handled for you: dual forms, the right agreement for each currency, and fractions such as halalas and fils. Choose مائة or مئة, add «فقط … لا غير» or "… Only", and copy the result with one click.
>
> Working in Excel or an accounting system? Copy a number, press Ctrl+Alt+T in any program, and the clipboard now holds the amount in words, ready to paste. To convert a whole column, paste it into the Bulk tab or import a CSV file, then copy the results or export them back to CSV.
>
> Print cheques and vouchers: a cheque layout with the amount in figures and in words with a dashed filler, receipt and payment vouchers on A5 or A4 with your company name and logo, and a words-only strip. A live preview shows exactly what prints, and per-field millimetre offsets line it up with your pre-printed cheques. Save as PDF with "Microsoft Print to PDF".
>
> The interface is in Arabic (right to left) or English and switches instantly. A native Windows app: fast, light and fully offline. No account, no ads, and no number ever leaves your computer. Free and open source (MIT).

**Short description**

> Turn any amount into correct words for cheques, invoices and vouchers, in Arabic, English or French. More than 50 currencies, cheque printing, whole Excel columns. Offline and free.

**Product features**

1. Amounts in words in Arabic, English and French
2. More than 50 currencies, with fractions such as halalas, fils and cents, and over 50 units of measure
3. Live conversion as you type, with Arabic-Indic (١٢٣) or Western digits
4. Correct Arabic grammar: dual forms, agreement, مائة or مئة
5. Ready phrases: «فقط … لا غير», "… Only", "Arrêtée la présente facture à la somme de …"
6. Ctrl+Alt+T from any program: the copied number becomes words in the clipboard
7. Bulk tab: paste a column from Excel or import CSV, export to CSV
8. Smooth with 10,000 rows
9. Cheque printing with the amount in figures and in words
10. Receipt and payment vouchers on A5 or A4, with your company name and logo
11. Live print preview, per-field calibration in millimetres, saved cheque profiles
12. Save as PDF with Microsoft Print to PDF
13. Arabic or English interface, switched instantly
14. History of recent conversions
15. Lives in the notification area; optional start with Windows
16. Works fully offline: no account, no ads, nothing sent anywhere
17. Free and open source (MIT)

**What's new in this version**

> First Microsoft Store release (version 2.2.1): amounts in words in Arabic, English and French, cheque and voucher printing with calibration and a print preview that fits the whole page, Bulk conversion of whole Excel columns, and the Ctrl+Alt+T clipboard shortcut. The Store keeps the app up to date.

**Search terms** (7): `tafqit` · `amount in words` · `cheque writer` · `number to words` · `تفقيط` · `المبلغ بالحروف` · `invoice`

**Copyright and trademark info:** © 2026 FastKiwi (Sarmad)

**Additional license terms:** https://github.com/sarmadiq9090-coder/InstaWords/blob/HEAD/LICENSE

<div dir="rtl">

### 8.2 العربية (Arabic)

**اسم المنتج:** InstaWords

**الوصف**

> InstaWords يكتب المبالغ بالحروف (التفقيط) للشيكات والفواتير وسندات القبض والصرف، بالعربية والإنجليزية والفرنسية، بأكثر من 50 عملة وأكثر من 50 وحدة أخرى (الوزن والطول والمساحة والوقت والكميات…).
>
> اكتب الرقم فيظهر المبلغ بالحروف فوراً أثناء الكتابة، بقواعد عربية صحيحة: المثنى، والتمييز المناسب لكل عملة، وأجزاء العملة (هللة، فلس، سنت…). اختر «مائة» أو «مئة»، وأضف «فقط … لا غير»، وانسخ النتيجة بنقرة.
>
> تعمل على Excel أو برنامج محاسبة؟ انسخ الرقم، واضغط Ctrl+Alt+T من أي برنامج، فيصبح ما نسخته (الحافظة) المبلغَ مكتوباً بالحروف جاهزاً للصق. ولتحويل عمود كامل، الصقه في تبويب «عدة مبالغ» أو استورد ملف CSV، ثم انسخ النتائج أو صدّرها CSV.
>
> طباعة الشيكات والسندات: شيك بالمبلغ بالأرقام وبالحروف مع الخط المتقطع بعده، وسند قبض وسند صرف على A5 أو A4 باسم مؤسستك وشعارها، وشريط للمبلغ بالحروف وحده. المعاينة الحيّة هي نفسها ما يُطبع، ومع المعايرة بالمليمتر لكل حقل يطابق الناتج شيكاتك المطبوعة مسبقاً. ولحفظ PDF اختر «Microsoft Print to PDF».
>
> الواجهة بالعربية (من اليمين إلى اليسار) أو بالإنجليزية، وتتبدّل فوراً. برنامج Windows أصلي، سريع وخفيف، يعمل كله دون إنترنت. لا حساب ولا إعلانات، ولا يغادر أي رقم جهازك. مجاني ومفتوح المصدر (MIT).

**الوصف المختصر**

> اكتب أي مبلغ بالحروف كتابةً صحيحة للشيكات والفواتير والسندات، بالعربية أو الإنجليزية أو الفرنسية. أكثر من 50 عملة، وطباعة الشيكات، وأعمدة Excel كاملة. يعمل دون إنترنت، ومجاني.

**المزايا (Product features)**

1. المبلغ بالحروف بالعربية والإنجليزية والفرنسية
2. أكثر من 50 عملة مع أجزائها مثل الهللة والفلس والسنت، وأكثر من 50 وحدة قياس
3. تفقيط فوري أثناء الكتابة، بالأرقام العربية (١٢٣) أو الإنجليزية
4. قواعد عربية صحيحة: المثنى والتمييز، و«مائة» أو «مئة»
5. صيغ جاهزة: «فقط … لا غير»، و"… Only"، والصيغة الفرنسية للفواتير
6. Ctrl+Alt+T من أي برنامج: الرقم الذي نسخته يصبح كلمات جاهزة للصق
7. تبويب «عدة مبالغ»: الصق عموداً من Excel أو استورد CSV، وصدّر النتائج CSV
8. سلس مع 10,000 سطر
9. طباعة الشيكات بالمبلغ بالأرقام وبالحروف
10. سند قبض وسند صرف على A5 أو A4 باسم مؤسستك وشعارها
11. معاينة حيّة للطباعة، ومعايرة بالمليمتر لكل حقل، وملفات شيكات محفوظة
12. حفظ PDF عبر Microsoft Print to PDF
13. واجهة بالعربية أو الإنجليزية تتبدّل فوراً
14. سجل لآخر التحويلات
15. يبقى بجانب الساعة في شريط المهام، مع تشغيل اختياري مع Windows
16. يعمل دون إنترنت: لا حساب ولا إعلانات ولا يُرسل شيئاً
17. مجاني ومفتوح المصدر (MIT)

**ما الجديد في هذا الإصدار (What's new)**

> أول إصدار في Microsoft Store (الإصدار 2.2.1): المبلغ بالحروف بالعربية والإنجليزية والفرنسية، وطباعة الشيكات والسندات مع المعايرة ومعاينة تعرض الصفحة كاملة، وتحويل أعمدة Excel كاملة في «عدة مبالغ»، واختصار Ctrl+Alt+T لتحويل ما نسخته. يحدّث المتجر البرنامج تلقائياً.

**كلمات البحث (Search terms)**، سبع كلمات بالضبط، كل واحدة في خانة:

تفقيط · كتابة المبلغ بالحروف · تحويل الأرقام إلى كلمات · المبلغ بالحروف · كتابة المبالغ · طباعة شيك · شيكات

**حقوق النشر (Copyright and trademark info):** ‏© 2026 FastKiwi (Sarmad)

**شروط الترخيص الإضافية:** https://github.com/sarmadiq9090-coder/InstaWords/blob/HEAD/LICENSE

## 9. الصور (Screenshots)

ثماني صور، أربع لكل لغة، كلها PNG بحجم 1920×1032 تقريباً (الحد الأدنى في المتجر 1366×768): النافذة مكبّرة وتملأ الشاشة دون سطح المكتب ودون شريط المهام، وكل مبلغ بالحروف وكل سطر في السجل من ناتج البرنامج نفسه. ارفعها بهذا الترتيب، ومع كل صورة تعليقها (خانة Caption، حتى 200 حرف):

**في الصفحة الإنجليزية (English):**

| # | الملف | التعليق (Caption) |
| --- | --- | --- |
| 1 | `store-en-1-convert.png` | Type an amount and the words appear as you type, ready for the cheque. Recent amounts stay in the history. |
| 2 | `store-en-2-print.png` | Print receipt and payment vouchers with your company name and logo, or fill in your pre-printed cheques. |
| 3 | `store-en-3-excel.png` | Paste a column from Excel and get every amount in words at once. Copy the results back or export them to CSV. |
| 4 | `store-en-4-settings.png` | Arabic or English interface, your default currency and wording, and Ctrl+Alt+T to convert a copied number in any program. |

**في الصفحة العربية:**

| # | الملف | التعليق |
| --- | --- | --- |
| 1 | `store-ar-1-convert.png` | اكتب المبلغ فيظهر بالحروف فوراً، جاهزاً للشيك. وآخر المبالغ محفوظة في السجل. |
| 2 | `store-ar-2-print.png` | اطبع سندات القبض والصرف باسم مؤسستك وشعارها، أو املأ شيكاتك المطبوعة مسبقاً. |
| 3 | `store-ar-3-excel.png` | الصق عموداً من Excel فتحصل على كل المبالغ بالحروف في خطوة واحدة. انسخ النتائج أو صدّرها إلى CSV. |
| 4 | `store-ar-4-settings.png` | واجهة بالعربية أو الإنجليزية، وعملتك وصيغتك الافتراضيتان، وCtrl+Alt+T لتحويل الرقم الذي نسخته من أي برنامج. |

ما في كل صورة: (1) تبويب التحويل وفيه مبلغ بالحروف مع «فقط … لا غير» وخمسة مبالغ في السجل؛ (2) نافذة الطباعة مكبّرة: سند قبض باسم مؤسسة وشعارها مع المعاينة الحيّة؛ (3) تبويب «عدة مبالغ» وفيه عمود ملصوق من Excel (عنوان وسبعة مبالغ) محوّل كله؛ (4) الإعدادات من أعلاها. صور اختبارات الواجهة تأتي من النسخة العادية، لذلك تظهر في صورة الإعدادات خيارات التحديث من GitHub، وفي نسخة المتجر يحلّ محلها «التحديثات تأتي من Microsoft Store». شعارات البرنامج يأخذها المتجر من الحزمة، فلا حاجة لرفع شعار.

## 10. Supplemental info ← Additional Testing Information

نقل Partner Center خانة «Notes for certification» إلى **Supplemental info ← Additional Testing Information**. الصق فيها هذا النص كما هو (بالإنجليزية لأن فريق الاعتماد يقرؤه):

</div>

> InstaWords is a classic WPF desktop application (.NET Framework 4.8, built into Windows 10 version 2004 and later and Windows 11) packaged as MSIX. It declares the restricted capability runFullTrust because it is a Win32/WPF desktop app (EntryPoint Windows.FullTrustApplication); it does not use any other restricted capability.
>
> - Global hotkey: the app registers Ctrl+Alt+T with RegisterHotKey (user-changeable or can be turned off in Settings). Pressing it converts a number on the clipboard to words and puts the words back on the clipboard. It only reads/writes the clipboard when the user presses the hotkey or a button.
> - Notification area: the app keeps a tray icon; closing the window keeps it running in the tray (a Settings option). "Exit" in the tray menu quits.
> - Start with Windows: off by default; uses the package's startup task (windows.startupTask) when the user enables it in Settings.
> - Printing: cheques and vouchers are printed with the standard Windows print dialog (no driver or service is installed).
> - No account, no sign-in, no ads, no telemetry, no in-app purchases.
> - Network: none. The Store build makes no network connections of its own: the GitHub update check of the non-Store build is disabled in the package (the Store updates the app). The only internet use is user-initiated links (website, source code, license, "Report a problem", which builds a prefilled GitHub issue URL) that open in the user's default browser.
> - Process.Start (flagged by the optional WACK "Blocked executables" test): used only to open https links in the user's default browser and the data folder in File Explorer, on the user's click. The non-Store build also uses it to run its downloaded installer; that code path is disabled in the package.
> - Data: settings and history are two local JSON files, plus a local crash log, under the user's AppData (redirected by MSIX to the package's LocalCache); nothing is sent anywhere.
> - Testing: no test account is needed. Type a number on the first screen; press Ctrl+P for the print preview; Ctrl+2 for bulk conversion.

<div dir="rtl">

**مبرّر القدرة المقيّدة runFullTrust:** يطلبه Partner Center لأن الحزمة تعلن runFullTrust، في خانة السؤال عن سبب الحاجة إلى القدرة (تظهر بعد رفع الحزمة، في Supplemental info أو Submission options بحسب نسخة اللوحة). الحد 500 حرف، وهذا النص **447** حرفاً:

</div>

> InstaWords is a classic WPF desktop app (.NET Framework 4.8) packaged as MSIX, so it needs runFullTrust like every packaged Win32 app. It uses it only for normal desktop behaviour: a global hotkey (Ctrl+Alt+T, RegisterHotKey), a notification-area icon, clipboard access when the user asks, printing through the Windows print dialog, and an optional startup task. It needs no other restricted capability and makes no network connections of its own.

<div dir="rtl">

## 11. Submission options

- **Publishing hold options:** اختر **Publish this submission as soon as it passes certification** (النشر تلقائياً بعد الاعتماد). هذا هو الخيار الافتراضي غالباً؛ تأكد منه فقط.
- لا شيء آخر هنا.

ثم ارجع إلى صفحة الإرسال واضغط **Submit to the Store**.

## 12. بعد الإرسال، والإصدارات القادمة

- يصلك بريد بنتيجة الاعتماد. إن رُفض الإرسال، تذكر الرسالة السبب والسياسة؛ أرسلها كما هي لتصحيح ما يلزم.
- بعد النشر: صفحة البرنامج https://apps.microsoft.com/detail/9P62P4WQSQNK، ويفتحها Windows مباشرة بالرابط `ms-windows-store://pdp/?ProductId=9P62P4WQSQNK`.
- **كل إرسال جديد يحتاج رقم حزمة أكبر** من آخر رقم رُفع (الآن 2.2.1.0)، والرقم الرابع يبقى 0. قبل كل إرسال:
  1. إن صار رقم `package.json` أكبر من 2.2.1 (إصدار عادي جديد)، احذف `storeVersion` من `windows/packaging/msix/identity.json`، فتتبع نسخة المتجر `package.json`.
  2. وإلا فارفع `storeVersion` (مثلاً `"2.2.2"` لإصلاح خاص بالمتجر).
  3. حدّث الرقم في اختبار `storeVersion` في `tests/msix.test.ts`، وشغّل `npm test`، وادفع التغيير، وانتظر نجاح GitHub Actions، ثم نزّل `store-package` الجديد وارفعه بـ **Update** على الإرسال الحالي في Partner Center.
- التحديث يصل المستخدمين من المتجر وحده: نسخة المتجر لا تحدّث نفسها.
- للدعم: **support@fastkiwi.app**.

## 13. ما لم يُتحقق منه بعد

- لم تُثبَّت الحزمة على جهاز حقيقي بعد: جُرّبت فقط على خادم Windows في GitHub بنسخة موقّعة بشهادة تجريبية. بعد النشر جرّب على جهازك: التشغيل مع Windows بعد تسجيل الدخول، وCtrl+Alt+T من Excel، والطباعة على طابعة حقيقية، و«فتح مجلد البيانات»، وحذف البرنامج.
- تحقق Partner Center من الحزمة نفسها لا يحدث إلا عند رفعها. أقرب فحص متاح هو WACK في GitHub Actions: النتيجة العامة PASS، مع تنبيه في الاختبار الاختياري «Blocked executables» (شرحه في ملاحظات الاعتماد أعلاه).
- إضافة Excel ‏(`InstaWords.xlam`) ليست في حزمة المتجر، ولا تُذكر في صفحته؛ تُنزَّل من إصدارات GitHub فقط.

</div>
